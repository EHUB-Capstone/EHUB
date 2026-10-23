"""Download pinned public inference artifacts; never reads or uploads CVs."""
import argparse
import hashlib
from pathlib import Path
import shutil
import urllib.request

MODEL_ID = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
REVISION = "e8f8c211226b894fcb81acc59f3b34ba3efd5f42"
FILES = {
    "onnx/model_quint8_avx2.onnx": "98a01d88b7de996cdea58c32ca71208c09968d143798814b2ea09d3439dc334f",
    "tokenizer.json": "2c3387be76557bd40970cec13153b3bbf80407865484b209e655e5e4729076b8",
}


def verify(path, digest):
    if not path.is_file():
        return False
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest() == digest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    for name, digest in FILES.items():
        path = args.output / name
        path.parent.mkdir(parents=True, exist_ok=True)
        if not verify(path, digest):
            temporary = path.with_suffix(path.suffix + ".download")
            try:
                request = urllib.request.Request(
                    f"https://huggingface.co/{MODEL_ID}/resolve/{REVISION}/{name}",
                    headers={"User-Agent": "EHub-local-mentor-semantic-setup"})
                with urllib.request.urlopen(request, timeout=120) as response, temporary.open("wb") as output:
                    shutil.copyfileobj(response, output)
                if not verify(temporary, digest):
                    raise ValueError("Public model artifact checksum mismatch")
                temporary.replace(path)
            finally:
                temporary.unlink(missing_ok=True)
        print(f"Verified {name}: {path.stat().st_size} bytes", flush=True)


if __name__ == "__main__":
    main()
