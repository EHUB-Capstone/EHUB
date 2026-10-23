"""Loopback-only, bounded HTTP interface to the offline multilingual ONNX model."""
import argparse
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
import math

from encoder import Encoder

SCHEMA = "ehub-mentor-semantic-v1"
MAX_BODY = 1024 * 1024


def strings(values, maximum):
    if not isinstance(values, list) or len(values) > maximum or any(
            not isinstance(x, str) or not x.strip() or len(x) > 1200 for x in values):
        raise ValueError("Invalid bounded text list")
    return values


def score(encoder, payload):
    if not isinstance(payload, dict) or payload.get("schema") != SCHEMA:
        raise ValueError("Unsupported protocol")
    project = strings(payload.get("project"), 12)
    candidates = payload.get("candidates")
    if not project or not isinstance(candidates, list) or len(candidates) > 32:
        raise ValueError("Invalid batch")
    ids = set()
    all_texts = list(project)
    entries = []
    for candidate in candidates:
        if not isinstance(candidate, dict):
            raise ValueError("Invalid candidate")
        identifier = candidate.get("id")
        if not isinstance(identifier, str) or not identifier.isascii() or not identifier.isdecimal() or identifier in ids:
            raise ValueError("Invalid anonymous candidate identifier")
        ids.add(identifier)
        evidence = strings(candidate.get("evidence"), 12)
        entries.append((identifier, len(all_texts), len(evidence)))
        all_texts.extend(evidence)
    vectors = encoder.encode(all_texts)
    project_vectors = vectors[:len(project)]
    items = []
    for identifier, start, count in entries:
        if count == 0:
            similarity, index = 0.0, -1
        else:
            # The best genuine experience excerpt supplies an inspectable explanation.
            matrix = vectors[start:start + count] @ project_vectors.T
            index = int(matrix.max(axis=1).argmax())
            # Require agreement with more than one project excerpt when possible;
            # one generic phrase about "developing a product" is insufficient.
            by_query = sorted(matrix.max(axis=0), reverse=True)
            similarity = max(-1.0, min(1.0, float(sum(by_query[:2]) / min(2, len(by_query)))))
        if not math.isfinite(similarity):
            raise RuntimeError("Invalid inference result")
        items.append({"id": identifier, "similarity": similarity, "evidenceIndex": index})
    return {"schema": SCHEMA, **encoder.identity, "items": items}


def handler_for(encoder):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *_):
            # Bodies, query strings and CV excerpts must never enter server logs.
            pass

        def setup(self):
            super().setup()
            self.connection.settimeout(5)

        def reply(self, status, body):
            data = json.dumps(body, ensure_ascii=False, allow_nan=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Cache-Control", "no-store")
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Connection", "close")
            self.end_headers()
            self.wfile.write(data)

        def do_GET(self):
            if self.path != "/health":
                return self.reply(404, {"error": "not_found"})
            self.reply(200, {"schema": SCHEMA, "ready": True, **encoder.identity})

        def do_POST(self):
            if self.path != "/score":
                return self.reply(404, {"error": "not_found"})
            # Backend-to-backend calls only; never allow websites to submit CV bodies.
            if self.headers.get("Origin") or self.headers.get_content_type() != "application/json":
                return self.reply(403, {"error": "backend_requests_only"})
            if self.headers.get("Transfer-Encoding"):
                return self.reply(400, {"error": "content_length_required"})
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if not 0 < length <= MAX_BODY:
                    return self.reply(413, {"error": "request_too_large"})
                data = self.rfile.read(length)
                if len(data) != length:
                    raise ValueError("Truncated request")
                payload = json.loads(data)
                result = score(encoder, payload)
            except (ValueError, TypeError, UnicodeError, TimeoutError):
                return self.reply(400, {"error": "invalid_request"})
            except Exception:
                return self.reply(503, {"error": "model_unavailable"})
            self.reply(200, result)

    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model-dir", required=True)
    parser.add_argument("--port", type=int, default=8091)
    parser.add_argument("--threads", type=int, choices=range(1, 5), default=2)
    args = parser.parse_args()
    encoder = Encoder(args.model_dir, threads=args.threads)
    server = HTTPServer(("127.0.0.1", args.port), handler_for(encoder))
    print(f"Mentor semantic model ready on 127.0.0.1:{args.port}; {encoder.identity}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
