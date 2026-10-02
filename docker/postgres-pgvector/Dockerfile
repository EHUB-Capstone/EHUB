FROM postgres:18-alpine

RUN apk add --no-cache --virtual .pgvector-build-deps build-base git \
    && git clone --depth 1 --branch v0.8.6 https://github.com/pgvector/pgvector.git /tmp/pgvector \
    && make -C /tmp/pgvector OPTFLAGS="" with_llvm=no \
    && make -C /tmp/pgvector install with_llvm=no \
    && rm -rf /tmp/pgvector \
    && apk del .pgvector-build-deps
