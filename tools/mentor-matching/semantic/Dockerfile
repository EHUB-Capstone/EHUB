FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

WORKDIR /opt/mentor-semantic
COPY tools/mentor-matching/semantic/requirements.txt ./requirements.txt
RUN python -m pip install --no-cache-dir -r requirements.txt

# Download a pinned public revision and fail the build if its hashes do not match.
COPY tools/mentor-matching/semantic/prepare_model.py ./prepare_model.py
RUN python prepare_model.py --output /opt/model

COPY tools/mentor-matching/semantic/encoder.py ./encoder.py
COPY tools/mentor-matching/semantic/server.py ./server.py

USER 65532:65532
ENTRYPOINT ["python", "server.py", "--model-dir", "/opt/model", "--port", "8091", "--threads", "2"]
