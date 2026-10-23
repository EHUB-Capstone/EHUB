# Triển khai Hybrid gợi ý mentor trên VPS

Tài liệu này hướng dẫn chạy provider `Hybrid` trên VPS Ubuntu 24.04 có 4 vCPU,
8 GB RAM và 50 GB SSD, dùng Docker Compose production hiện có của E-HUB.
Hybrid kết hợp ranker tự train v2 với encoder ONNX chạy cục bộ. Đây là cấu hình
pilot: encoder và ngưỡng Hybrid chưa được kiểm chứng trên nhãn ghép mentor thực,
nên kết quả vẫn chỉ là gợi ý để người phụ trách xem xét.

## 1. Hybrid cần những gì

- CPU hỗ trợ AVX2. Model ONNX đã lượng tử hóa, file model khoảng 119 MB; không
  cần GPU, Python cài trên host, API key AI hoặc dịch vụ AI trả phí.
- Một container Python 3.12 chạy encoder trên loopback `127.0.0.1:8091`.
- Backend chạy `Mentoring:MatchingProvider=Hybrid` và gọi encoder qua cùng
  loopback. Vì backend đang ở Docker, encoder dùng chung network namespace với
  backend bằng `network_mode: service:backend`; không mở cổng encoder ra ngoài.
- Database production đã dùng image PostgreSQL có pgvector và đã áp dụng các
  migration của release. Không xóa hoặc tạo lại volume database.

Hybrid gửi sang encoder các đoạn tên/mô tả dự án và bằng chứng kinh nghiệm đã
lọc; encoder không nhận CV gốc, email, tên, ID tài khoản hay tổ chức. Model được
tải trong lúc build từ revision cố định và xác minh SHA-256; suy luận không cần
Internet.

## 2. Kiểm tra trước khi deploy

Thêm Dockerfile và Compose override bên dưới vào branch, review và deploy cùng
release. Không tạo các file này trực tiếp trong production checkout. Trước khi
cập nhật production, làm backup và kiểm tra khả năng restore theo
[PRODUCTION_VPS.md](PRODUCTION_VPS.md). Không gửi `.env.production` hoặc giá trị
secret vào log/chat.

```bash
cd /opt/ehub/app
grep -qm1 -w avx2 /proc/cpuinfo && echo 'AVX2 supported' || echo 'AVX2 unavailable'
free -h
df -h /
docker compose version
```

Nếu CPU không có AVX2, dừng ở đây và dùng provider `Trained` hoặc `Ollama`.

## 3. Tạo Dockerfile cho semantic service

Tạo `tools/mentor-matching/semantic/Dockerfile` trong branch chuẩn bị release:

```dockerfile
FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

WORKDIR /opt/mentor-semantic
COPY tools/mentor-matching/semantic/requirements.txt ./requirements.txt
RUN python -m pip install --no-cache-dir -r requirements.txt

# prepare_model downloads a pinned public revision and refuses checksum mismatches.
COPY tools/mentor-matching/semantic/prepare_model.py ./prepare_model.py
RUN python prepare_model.py --output /opt/model

COPY tools/mentor-matching/semantic/encoder.py ./encoder.py
COPY tools/mentor-matching/semantic/server.py ./server.py

USER 65532:65532
ENTRYPOINT ["python", "server.py", "--model-dir", "/opt/model", "--port", "8091", "--threads", "2"]
```

Model nằm trong image semantic; không cần volume chứa model. Build cần truy cập
Hugging Face để tải đúng revision đã khóa trong `prepare_model.py`. Nếu tải hoặc
checksum thất bại thì build phải dừng; không bỏ qua bước xác minh.

## 4. Tạo Compose override cho Hybrid

Tạo `docker-compose.hybrid.yml` ở thư mục gốc branch chuẩn bị release:

```yaml
services:
  backend:
    environment:
      Mentoring__MatchingProvider: Hybrid
      Mentoring__Semantic__BaseUrl: http://127.0.0.1:8091/

  mentor-semantic:
    image: ehub-mentor-semantic:${IMAGE_TAG:-local}
    build:
      context: .
      dockerfile: tools/mentor-matching/semantic/Dockerfile
    restart: unless-stopped
    network_mode: "service:backend"
    depends_on:
      backend:
        condition: service_started
    cpus: "2.0"
    mem_limit: 2g
    memswap_limit: 2g
    healthcheck:
      test:
        - CMD
        - python
        - -c
        - "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8091/health', timeout=3)"
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 60s
    logging:
      driver: local
      options:
        max-size: "10m"
        max-file: "5"
    security_opt:
      - no-new-privileges:true
    init: true
```

`network_mode: service:backend` là phần cần thiết: code backend chỉ chấp nhận
loopback HTTP cho semantic service. Không thêm `ports`, `expose`, mạng riêng
hoặc Nginx route cho `8091`. Service không có healthcheck trong backend
readiness, vì vậy phải kiểm tra nó riêng sau khi khởi động.

## 5. Build, migrate và khởi động

Dùng cùng `IMAGE_TAG` release đã review. Các lệnh sau kết hợp Compose production
với override Hybrid; thay đổi `.env.production` chỉ trên VPS, không commit file
này.

```bash
cd /opt/ehub/app
dc=(sudo docker compose --env-file .env.production \
    -f docker-compose.production.yml -f docker-compose.hybrid.yml)
"${dc[@]}" config --quiet
"${dc[@]}" build postgres backend frontend mentor-semantic
"${dc[@]}" up -d postgres
"${dc[@]}" run --rm backend --initialize-database
"${dc[@]}" up -d --force-recreate backend mentor-semantic frontend
"${dc[@]}" ps
```

Initializer chỉ chạy một lần theo quy trình production và phải thoát thành
công. Nếu migration lỗi, dừng deploy và làm theo runbook chính; không sửa schema
thủ công. Không cần chạy `ollama pull bge-m3` để dùng Hybrid.

Sau khi xác nhận Hybrid hoạt động, có thể dừng Ollama để dành RAM cho ứng dụng
trên VPS 8 GB. Giữ nguyên volume Ollama để có thể quay lui:

```bash
"${dc[@]}" stop ollama
```

Nếu các chức năng khác trên instance đang dùng Ollama, không dừng service đó;
theo dõi RAM và giảm tải đồng thời trước khi bật cho người dùng rộng hơn.

## 6. Kiểm tra sau triển khai

```bash
"${dc[@]}" ps
"${dc[@]}" exec backend curl --fail --silent --show-error http://127.0.0.1:8091/health
curl --fail --silent --show-error \
  --header 'Host: e-hub.com.vn' \
  http://127.0.0.1:3000/health/ready
sudo ss -lntup
free -h
sudo docker stats --no-stream
```

Health của semantic service phải trả `ready: true` cùng model
`sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` và revision đúng
như trong `tools/mentor-matching/semantic/README.md`. Cổng 8091 không được xuất
hiện như cổng public của VPS.

Tiếp đó, trong giao diện, chạy gợi ý với một nhóm có tên đề tài, mô tả, mentor
đang hoạt động và đã được gán vào học kỳ. Kiểm tra lý do gợi ý có bằng chứng
kinh nghiệm cụ thể. Sau đó xem log giới hạn:

```bash
"${dc[@]}" logs --tail 100 mentor-semantic
"${dc[@]}" logs --tail 100 backend
```

Server semantic cố ý không log nội dung request. Nếu service chưa healthy,
model sai revision, hoặc request lỗi, Hybrid sẽ báo dịch vụ gợi ý tạm thời chưa
sẵn sàng; nó không tự âm thầm đổi sang provider khác.

## 7. Quay lui

Để quay về ranker tự train v2, cập nhật override thành:

```yaml
services:
  backend:
    environment:
      Mentoring__MatchingProvider: Trained
```

Sau đó recreate backend và dừng semantic service:

```bash
"${dc[@]}" up -d --no-deps --force-recreate backend
"${dc[@]}" stop mentor-semantic
```

`Trained` dùng model bootstrap đã đóng gói trong backend, không cần Ollama hay
semantic service. Muốn quay về provider mặc định `Ollama`, đổi giá trị thành
`Ollama`, chạy `"${dc[@]}" up -d ollama backend`, rồi xác nhận model `bge-m3`
đang có trong volume bằng `ollama list`.

## 8. Giới hạn và theo dõi chất lượng

Hybrid vẫn là thử nghiệm: encoder MiniLM được huấn luyện sẵn bên ngoài, chưa
fine-tune bằng nhãn E-HUB; blend/ngưỡng được chọn trên dữ liệu giả lập. Điểm số
không phải xác suất chính xác. Chạy pilot với giảng viên, lưu phản hồi phù hợp /
chưa phù hợp theo quy trình bảo vệ dữ liệu của dự án, rồi đánh giá độc lập
Top-1/Top-3, NDCG và các trường hợp không nên gợi ý trước khi xem xét bật rộng.
Không đưa CV hoặc dữ liệu production có thông tin cá nhân vào Git, fixture hay
log.

Với 4 vCPU/8 GB, cấu hình ban đầu giới hạn semantic service ở 2 vCPU, 2 GB RAM
và hai luồng suy luận. Đây là mức khởi đầu, chưa phải benchmark hay SLA. Theo
dõi RAM/CPU và độ trễ cùng PostgreSQL, backend, frontend; nếu VPS thiếu RAM hoặc
CPU, quay về `Trained` trong lúc đánh giá.
