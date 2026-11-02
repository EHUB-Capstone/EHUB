# Triển khai AI chia mentor bằng CPU

Chọn `Trained` để chạy model xếp hạng ngay trong backend .NET. Không cần API key AI,
GPU, Python, Ollama hoặc container semantic cho chế độ này. Luồng sử dụng là
**AI tạo bản phân công → admin xem/sửa → Accept and assign mentors để chốt**.

## 1. Những gì đã có trong Git

- `backend/src/EHub.Infrastructure/Mentoring/Models/allocation-model.json`: đúng artifact
  đã dùng trong demo, schema `ehub-mentor-fit-v2`, 43 chủ đề và 8 đặc trưng.
- Infrastructure sao chép file vào output và publish. Dockerfile hiện có lấy output
  publish nên image backend chứa `/app/Mentoring/Models/allocation-model.json`.
- `docker-compose.trained.yml` chọn provider và đường dẫn trên. Không cần bind mount
  hoặc copy model thủ công lên VPS.
- Script deploy staging/production nhận `MENTOR_MATCHING_PROVIDER=Trained`, tự thêm
  override và bỏ qua bước khởi động Ollama/tải `bge-m3`.

SHA-256 của model:

```text
de1e87462b7a49487d4eebe847ff429f79831ad77e33bf81773456394349679b
```

Artifact chỉ chứa danh mục, hệ số và metadata. CV gốc, tài khoản, email, hồ sơ nguồn
và dữ liệu demo không được mang lên production qua gói này. Model dùng nhãn mô phỏng,
`suitabilityValidated: false`; kết quả demo không phải độ chính xác trên dự án thật.

## 2. Người triển khai thực hiện

1. Review thay đổi trên nhánh `AI`, đưa vào `develop` để deploy staging; release vào
   `main` để deploy production theo [quy trình hiện có](RELEASE_PROCESS.md).
   Script giữ kiểm tra nhánh, backup và staging gate; không deploy trực tiếp SHA chỉ có trên `AI`.
2. **Cài lại hai script deploy đã review trên VPS.** Bản root-owned ở
   `/opt/ehub/deploy/` không tự đổi khi pull Git. Gửi hai file mới tới `/tmp/`, rồi chạy:

   ```bash
   bash -n /tmp/deploy-staging.sh
   bash -n /tmp/deploy-production.sh
   sudo install -m 0755 -o root -g root /tmp/deploy-staging.sh /opt/ehub/deploy/deploy-staging.sh
   sudo install -m 0755 -o root -g root /tmp/deploy-production.sh /opt/ehub/deploy/deploy-production.sh
   ```

   Xem cách gửi file trong [STAGING_CD.md](STAGING_CD.md) và [PRODUCTION_CD.md](PRODUCTION_CD.md).
   Entry script và sudoers không cần đổi cho lần này.
3. Trong `.env.staging` hoặc `.env.production` **chỉ trên VPS**, đặt đúng một dòng:

   ```dotenv
   MENTOR_MATCHING_PROVIDER=Trained
   ```

   Giữ nguyên các cấu hình database, storage, email và xác thực đang dùng.
   File `.env.*.example` giữ mặc định Ollama cho các môi trường chưa chuyển provider.
4. Deploy qua workflow hiện có. Backend/frontend được build từ commit release,
   model đi cùng image backend. Thay đổi AI lần này không thêm migration;
   script vẫn chạy các migration khác thuộc release theo quy trình hiện có.

Khi chạy Compose thủ công, `MENTOR_MATCHING_PROVIDER` là lựa chọn của **script deploy**,
không tự kích hoạt override. Các lệnh cho backend phải thêm
`-f docker-compose.trained.yml` sau file Compose của môi trường.

## 3. Kiểm tra sau deploy

Log deploy phải có `AI provider: Trained`. Ví dụ kiểm tra trên production:

```bash
cd /opt/ehub/app
sudo docker compose --project-name ehub-production --env-file .env.production \
  -f docker-compose.production.yml -f docker-compose.trained.yml \
  exec -T backend printenv Mentoring__MatchingProvider Mentoring__TrainedModelPath
sudo docker compose --project-name ehub-production --env-file .env.production \
  -f docker-compose.production.yml -f docker-compose.trained.yml \
  exec -T backend sha256sum /app/Mentoring/Models/allocation-model.json
```

Kết quả phải là `Trained`, đúng đường dẫn và checksum ở mục 1. Chỉ kiểm tra hai
biến trên; không xuất toàn bộ environment hoặc cấu hình Compose chứa secret.

Đăng nhập admin, bảo đảm mentor có tài khoản/hồ sơ hoạt động và tham gia kỳ đang chọn,
sau đó vào **Subject Management → Lecturers & Mentors → AI preview**. Tên/mô tả dự án,
hồ sơ từ CV và lịch sử/thành tích hiện có trong database được dùng để đề xuất.
Preview không ghi phân công; admin bấm chốt mới lưu. Điểm 0 vẫn cần xem lý do.
`/health/ready` không kiểm tra chất lượng hoặc nạp model, nên phải thử AI preview thực tế.

Script không tự dừng container Ollama đã chạy từ trước; việc đổi provider không
giải phóng RAM của container đó. Nếu Ollama còn phục vụ chức năng khác, giữ nó theo nhu cầu.
Quay lại `Ollama`/`Hybrid` bằng cách đổi một dòng provider rồi deploy lại; không sửa
database hoặc xóa volume. Model tùy chỉnh sau này có thể dùng `Mentoring__TrainedModelPath`
với file và mount riêng; cần tạo lại backend khi thay artifact.

## 4. Kiểm tra gói ngày 02/11/2026

- Backend build thành công, 0 lỗi; còn 7 cảnh báo sẵn có.
- `dotnet test EHub.slnx --no-build --no-restore --filter "FullyQualifiedName!~IntegrationTests"`:
  141 UnitTests và 726 ApplicationTests đạt, gồm kiểm tra checksum và xếp hạng bằng model đóng gói.
- `dotnet publish src/EHub.Api/EHub.Api.csproj --no-restore -c Release`: output API có
  `Mentoring/Models/allocation-model.json` với checksum khớp mục 1.
- Sáu cấu hình Compose (production/staging, mặc định/Trained/Hybrid) đều qua `config --quiet`;
  hai script qua `bash -n`. Kiểm tra cục bộ 16 trường hợp lựa chọn provider/dependency bằng
  Docker giả lập đều đạt, không chạy lệnh deploy thật.
- Chưa chạy lại integration test hoặc build image Docker: Docker engine local chưa sẵn sàng,
  WSL không có distribution đang chạy. Frontend không thay đổi trong gói này, không chạy lại test frontend.
- Chưa triển khai lên staging/production. CI và kiểm tra AI preview sau deploy vẫn cần thực hiện.
