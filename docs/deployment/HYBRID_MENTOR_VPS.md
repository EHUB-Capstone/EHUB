# Triển khai Hybrid gợi ý mentor trên VPS

Tài liệu này hướng dẫn chạy provider `Hybrid` trên VPS Ubuntu 24.04 có 4 vCPU,
8 GB RAM và 50 GB SSD, dùng chung quy trình deploy tự động của E-HUB
([STAGING_CD.md](STAGING_CD.md), [PRODUCTION_CD.md](PRODUCTION_CD.md)).
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
- Ollama và bảng `mentor_embeddings` vẫn được giữ: chúng là provider mặc định và là
  đường quay lui. Script deploy tiếp tục khởi động Ollama như trước.

Hybrid gửi sang encoder các đoạn tên/mô tả dự án và bằng chứng kinh nghiệm đã
lọc; encoder không nhận CV gốc, email, tên, ID tài khoản hay tổ chức. Model được
tải trong lúc build từ revision cố định và xác minh SHA-256; suy luận không cần
Internet.

## 2. Kiểm tra trước khi bật

Trước khi bật, làm backup và kiểm tra khả năng restore theo
[PRODUCTION_VPS.md](PRODUCTION_VPS.md) (script deploy production tự backup và upload R2).
Không gửi `.env.production` hoặc giá trị secret vào log/chat.

```bash
grep -qm1 -w avx2 /proc/cpuinfo && echo 'AVX2 supported' || echo 'AVX2 unavailable'
free -h
df -h /
docker compose version
sudo docker stats --no-stream
```

Nếu CPU không có AVX2, dừng ở đây và dùng provider `Ollama`. VPS không có swap, nên
`available` trong `free -h` là con số cần xem, không phải `free`. Script deploy tự từ chối
bật Hybrid khi RAM khả dụng dưới 1536 MB.

## 3. Các file đã có trong repo

Không tạo các file này bằng tay trên VPS: chúng được review và deploy cùng code.

- `tools/mentor-matching/semantic/Dockerfile`: image của semantic service (Python 3.12,
  model được tải và kiểm SHA-256 lúc build, chạy bằng user không phải root).
- `docker-compose.hybrid.yml`: override đặt `Mentoring__MatchingProvider=Hybrid`,
  `Mentoring__Semantic__BaseUrl=http://127.0.0.1:8091/` và định nghĩa container
  `mentor-semantic` (giới hạn 2 CPU, 2 GB RAM, healthcheck, `network_mode: service:backend`).

## 4. Bật hoặc tắt Hybrid bằng công tắc

Provider được chọn riêng cho từng môi trường bằng **một dòng trong file env trên VPS**
(`/opt/ehub/staging/app/.env.staging` hoặc `/opt/ehub/app/.env.production`):

```text
MENTOR_MATCHING_PROVIDER=Ollama
```

- `Ollama` (hoặc không có dòng này): hành vi mặc định, không có `mentor-semantic`.
- `Hybrid`: script ghép thêm `docker-compose.hybrid.yml`.
- Giá trị khác, hoặc dòng bị lặp: script dừng ngay ở bước `preconditions`, chưa đổi gì.

Đổi dòng này bằng `nano`, rồi chạy workflow deploy (Deploy Staging hoặc Deploy Production)
**với SHA đang chạy** để áp dụng. Không dùng lệnh `docker compose` thủ công để bật Hybrid:
lần deploy kế tiếp sẽ ghi đè.

Khi `Hybrid` được bật, mỗi lần deploy script tự làm:

1. Từ chối nếu RAM khả dụng dưới 1536 MB (VPS không có swap).
2. Build `mentor-semantic` cùng backend và frontend (model được dùng lại từ cache Docker
   dùng chung giữa staging và production; nếu cache đã bị dọn thì tải lại và kiểm SHA-256).
3. Gỡ container `mentor-semantic` cũ, rồi tạo backend và `mentor-semantic` mới cùng lúc.
   Lý do: container này dùng chung mạng với backend, nên nếu backend được tạo lại mà nó
   không được tạo lại, nó vẫn ở trạng thái "Up" nhưng **không còn nối được tới backend**.
4. Kiểm tra riêng `http://127.0.0.1:8091/health` từ bên trong backend: `ready: true` và
   đúng revision model ghi trong `prepare_model.py` của commit đang deploy. Sức khỏe của
   backend (`/health/ready`) **không** bao gồm dịch vụ này.
5. Xác nhận image `mentor-semantic` đang chạy đúng tag của lần deploy.
6. Ghi provider vào log và thông báo Discord ("AI provider: Hybrid").

Mọi lệnh Compose trong script dùng `--no-deps` cho bước migrate và khởi động ứng dụng, nên
một thay đổi trong định nghĩa `postgres` hoặc `ollama` không thể làm tạo lại container
database trong lúc deploy. **Không chạy thủ công `build postgres`, `up -d postgres` hay
`up --force-recreate` không kèm `--no-deps` trên production**: Compose sẽ tạo lại container
database nếu định nghĩa của nó lệch so với container đang chạy.

## 5. Kiểm tra sau triển khai

```bash
sudo docker compose --project-name ehub-staging --env-file /opt/ehub/staging/app/.env.staging \
  -f /opt/ehub/staging/app/docker-compose.staging.yml \
  -f /opt/ehub/staging/app/docker-compose.hybrid.yml ps
sudo docker compose --project-name ehub-staging --env-file /opt/ehub/staging/app/.env.staging \
  -f /opt/ehub/staging/app/docker-compose.staging.yml \
  -f /opt/ehub/staging/app/docker-compose.hybrid.yml \
  exec -T backend curl --fail --silent --show-error http://127.0.0.1:8091/health
sudo ss -lntup
free -h
sudo docker stats --no-stream
```

(Đổi `ehub-staging` và các đường dẫn staging thành `ehub-production` và `/opt/ehub/app`
cho production.) Health của semantic service phải trả `ready: true` cùng model
`sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` và revision đúng như trong
`tools/mentor-matching/semantic/README.md`. Cổng 8091 không được xuất hiện như cổng public
của VPS.

Tiếp đó, trong giao diện, chạy gợi ý với một nhóm có tên đề tài, mô tả, mentor đang hoạt
động và đã được gán vào học kỳ. Kiểm tra lý do gợi ý có bằng chứng kinh nghiệm cụ thể. Sau
đó xem log giới hạn:

```bash
sudo docker compose --project-name ehub-staging --env-file /opt/ehub/staging/app/.env.staging \
  -f /opt/ehub/staging/app/docker-compose.staging.yml \
  -f /opt/ehub/staging/app/docker-compose.hybrid.yml logs --tail 100 mentor-semantic
```

Server semantic cố ý không log nội dung request. Nếu service chưa healthy, model sai
revision, hoặc request lỗi, Hybrid sẽ báo dịch vụ gợi ý tạm thời chưa sẵn sàng; nó không tự
âm thầm đổi sang provider khác.

## 6. Quay lui

Đổi `MENTOR_MATCHING_PROVIDER=Ollama` (hoặc xóa dòng) trong file env của môi trường rồi
chạy lại workflow deploy với SHA đang chạy. Script tự gỡ container `mentor-semantic` còn lại
và tạo backend theo provider mặc định. Ollama, model `bge-m3` và bảng `mentor_embeddings`
không bị xóa trong suốt quá trình nên đường lui luôn mở. Thao tác mất khoảng hai phút.

Provider `Trained` (không cần container) hiện **không** nằm trong công tắc triển khai; cần
một thay đổi riêng nếu muốn dùng.

## 7. Pilot trên staging (đã thống nhất với người phụ trách)

- Staging chạy Hybrid tối đa 2 tuần, với ít nhất 50 dự án giả lập, hai giảng viên hoặc
  người nghiệm thu đánh giá độc lập. Không dùng CV hoặc dữ liệu cá nhân thật.
- Chạy **baseline Ollama trước** (staging đang ở Ollama), rồi bật Hybrid và chạy lại **đúng
  bộ dữ liệu đó**, vì một môi trường chỉ chạy một provider tại một thời điểm.
- Ngưỡng đạt: Top-1 ≥ 60%, Top-3 ≥ 80%, từ chối đúng ca không phù hợp ≥ 80%, và Top-3 không
  thấp hơn Ollama quá 5 điểm phần trăm trên cùng bộ dữ liệu.
- Ngưỡng vận hành đề xuất (chờ người phụ trách xác nhận): không có lỗi "chưa sẵn sàng" khi
  dùng bình thường; RAM khả dụng của VPS không dưới 1,5 GB khi chạy đồng thời; độ trễ gợi ý
  chốt sau lần đo trước pilot.
- Timeout của encoder (hiện 40 giây trong luồng 45 giây) được giảm xuống 20 giây trước pilot,
  và tải đồng thời được đo trước khi bắt đầu.
- Trong các phiên chấm, đặt biến GitHub `STAGING_AUTO_DEPLOY=false`: mỗi lần deploy tạo lại
  `mentor-semantic` và mất khoảng một phút để sẵn sàng.
- Giữ ngưỡng 0,50, margin 0,08, weight 0,25. Điều chỉnh cần người phụ trách đánh giá đề xuất
  qua PR kèm số liệu.
- Cách chạy AI phải được **nghiệm thu trên staging trước khi đưa lên production**. Nếu pilot
  đạt, đặt công tắc `Hybrid` ở production trong một release thường. Nếu không đạt, đưa
  staging về `Ollama` để staging giống production.

## 8. Giới hạn và theo dõi chất lượng

Hybrid vẫn là thử nghiệm: encoder MiniLM được huấn luyện sẵn bên ngoài, chưa
fine-tune bằng nhãn E-HUB; blend/ngưỡng được chọn trên dữ liệu giả lập. Điểm số
không phải xác suất chính xác. Chạy pilot với giảng viên, lưu phản hồi phù hợp /
chưa phù hợp theo quy trình bảo vệ dữ liệu của dự án, rồi đánh giá độc lập
Top-1/Top-3, NDCG và các trường hợp không nên gợi ý trước khi xem xét bật rộng.
Không đưa CV hoặc dữ liệu production có thông tin cá nhân vào Git, fixture hay
log.

Với 4 vCPU/8 GB dùng chung cho staging và production, cấu hình ban đầu giới hạn semantic
service ở 2 vCPU, 2 GB RAM và hai luồng suy luận cho **mỗi** môi trường. Đây là mức khởi
đầu, chưa phải benchmark hay SLA (tài liệu của nhóm AI ghi working set khoảng 446 MB đo trên
PC, không phải peak hay số đo trên VPS). Theo dõi RAM/CPU và độ trễ cùng PostgreSQL, backend,
frontend; nếu VPS thiếu RAM hoặc CPU, quay về `Ollama` trong lúc đánh giá.
