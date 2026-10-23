"""Offline CPU encoder. No Hugging Face API calls occur during inference."""
from collections import OrderedDict
from pathlib import Path
import threading

import numpy as np
import onnxruntime as ort
from tokenizers import Tokenizer

from prepare_model import FILES, MODEL_ID, REVISION, verify


class Encoder:
    def __init__(self, model_dir, threads=2, cache_size=512):
        model_dir = Path(model_dir)
        if not 1 <= threads <= 4 or not 0 <= cache_size <= 2048:
            raise ValueError("Invalid encoder resource limits")
        for name, digest in FILES.items():
            if not verify(model_dir / name, digest):
                raise ValueError("Pinned local model artifacts are missing or invalid; run prepare_model.py")
        options = ort.SessionOptions()
        options.intra_op_num_threads = threads
        options.inter_op_num_threads = 1
        self.session = ort.InferenceSession(str(model_dir / "onnx/model_quint8_avx2.onnx"),
                                            sess_options=options, providers=["CPUExecutionProvider"])
        self.tokenizer = Tokenizer.from_file(str(model_dir / "tokenizer.json"))
        self.tokenizer.enable_truncation(max_length=128)
        pad_token = "<pad>" if self.tokenizer.token_to_id("<pad>") is not None else "[PAD]"
        self.tokenizer.enable_padding(pad_id=self.tokenizer.token_to_id(pad_token), pad_token=pad_token)
        self.cache = OrderedDict()
        self.cache_size = cache_size
        self.lock = threading.Lock()

    def encode(self, texts):
        if not texts:
            return np.empty((0, 384), dtype=np.float32)
        if len(texts) > 2048 or any(not isinstance(x, str) or not x or len(x) > 1200 for x in texts):
            raise ValueError("Encoder input exceeds limits")
        # The lock also prevents concurrent tokenizer mutation / excess CPU inference.
        with self.lock:
            missing = list(dict.fromkeys(x for x in texts if x not in self.cache))
            fresh = {}
            for start in range(0, len(missing), 16):
                batch = missing[start:start + 16]
                encoded = self.tokenizer.encode_batch(batch)
                available = {
                    "input_ids": np.asarray([x.ids for x in encoded], dtype=np.int64),
                    "attention_mask": np.asarray([x.attention_mask for x in encoded], dtype=np.int64),
                    "token_type_ids": np.asarray([x.type_ids for x in encoded], dtype=np.int64),
                }
                outputs = self.session.run(None, {x.name: available[x.name] for x in self.session.get_inputs()})
                tokens = outputs[0]
                mask = available["attention_mask"][..., None]
                vectors = (tokens * mask).sum(axis=1) / np.maximum(mask.sum(axis=1), 1)
                vectors /= np.maximum(np.linalg.norm(vectors, axis=1, keepdims=True), 1e-12)
                fresh.update(zip(batch, vectors.astype(np.float32)))
            result = np.stack([fresh[x] if x in fresh else self.cache[x] for x in texts])
            # Cache contains only bounded in-memory text; never persisted or logged.
            for text in texts:
                self.cache[text] = fresh[text] if text in fresh else self.cache[text]
                self.cache.move_to_end(text)
            while len(self.cache) > self.cache_size:
                self.cache.popitem(last=False)
            return result

    @property
    def identity(self):
        return {"model": MODEL_ID, "revision": REVISION, "runtime": "onnx-cpu-int8", "dimensions": 384}
