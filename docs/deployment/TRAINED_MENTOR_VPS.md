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
2. **Cài lại script deploy đã review trên VPS, trước khi đổi provider.** Bản root-owned ở
   `/opt/ehub/deploy/` không tự đổi khi pull Git. Script cũ chỉ nhận `Ollama|Hybrid`: nếu đổi
   `.env` trước, deploy dừng ở bước `preconditions` (an toàn, chưa backup/checkout, nhưng thất bại).
   Kiểm tra bản đang cài, kết quả `0` là bản cũ:

   ```bash
   grep -c -i trained /opt/ehub/deploy/deploy-staging.sh
   grep -c verify-ai-model /opt/ehub/deploy/deploy-staging.sh
   ```

   Khi commit đã nằm trong checkout staging (đã merge vào `develop` và staging đã deploy),
   cài thẳng từ checkout đó; tree production phải bằng staging nên dùng cùng nguồn:

   ```bash
   bash -n /opt/ehub/staging/app/scripts/staging/deploy-staging.sh
   bash -n /opt/ehub/staging/app/scripts/production/deploy-production.sh
   sudo install -m 0755 -o root -g root /opt/ehub/staging/app/scripts/staging/deploy-staging.sh /opt/ehub/deploy/deploy-staging.sh
   sudo install -m 0755 -o root -g root /opt/ehub/staging/app/scripts/production/deploy-production.sh /opt/ehub/deploy/deploy-production.sh
   ```

   Khi chưa merge, gửi file đã review tới `/tmp/` như [STAGING_CD.md](STAGING_CD.md) và
   [PRODUCTION_CD.md](PRODUCTION_CD.md). Entry script và sudoers không cần đổi.
3. Trong `.env.staging` hoặc `.env.production` **chỉ trên VPS**, **sửa** dòng provider đang có
   (file đã có `MENTOR_MATCHING_PROVIDER=Ollama`; thêm dòng thứ hai làm deploy thất bại):

   ```bash
   sudo sed -i 's/^MENTOR_MATCHING_PROVIDER=.*/MENTOR_MATCHING_PROVIDER=Trained/' /opt/ehub/staging/app/.env.staging
   sudo grep -c '^MENTOR_MATCHING_PROVIDER=' /opt/ehub/staging/app/.env.staging
   ```

   Lệnh thứ hai phải in `1`. Với production thay đường dẫn bằng `/opt/ehub/app/.env.production`.
   Giữ nguyên các cấu hình database, storage, email và xác thực đang dùng.
   File `.env.*.example` giữ mặc định Ollama cho các môi trường chưa chuyển provider.
4. **Chạy deploy thủ công.** Sửa `.env` không phải là push nên không kích hoạt workflow:
   GitHub Actions → **Deploy Staging** → **Run workflow** trên `develop` (để trống SHA);
   production dùng **Deploy Production** với SHA đang chạy. Backend/frontend được build từ
   commit release, model đi cùng image backend. Không có migration cho thay đổi AI.

Với production, nên tách hai lần: release code khi vẫn để `Ollama`, kiểm tra ổn định,
sau đó mới đổi dòng provider và deploy lại cùng SHA.

Khi chạy Compose thủ công, `MENTOR_MATCHING_PROVIDER` là lựa chọn của **script deploy**,
không tự kích hoạt override. Các lệnh cho backend phải thêm
`-f docker-compose.trained.yml` sau file Compose của môi trường.

## 3. Kiểm tra sau deploy

Log deploy phải có `AI provider: Trained` và `ok mentor model (sha256 de1e87462b7a)` ở bước
`verify-ai-model`. Bước này chạy bằng user không phải root của backend và so checksum với commit;
nếu thất bại, Discord báo `Failed at stage: verify-ai-model` trong khi site vẫn chạy.

Kiểm tra thủ công trên staging:

```bash
cd /opt/ehub/staging/app
sudo docker compose --project-name ehub-staging --env-file .env.staging \
  -f docker-compose.staging.yml -f docker-compose.trained.yml \
  exec -T backend sh -c 'printenv Mentoring__MatchingProvider Mentoring__TrainedModelPath; ls -ln /app/Mentoring/Models/; sha256sum /app/Mentoring/Models/allocation-model.json'
```

Trên production:

```bash
cd /opt/ehub/app
sudo docker compose --project-name ehub-production --env-file .env.production \
  -f docker-compose.production.yml -f docker-compose.trained.yml \
  exec -T backend sh -c 'printenv Mentoring__MatchingProvider Mentoring__TrainedModelPath; ls -ln /app/Mentoring/Models/; sha256sum /app/Mentoring/Models/allocation-model.json'
```

Kết quả phải là `Trained`, đúng đường dẫn, file `-rw-r--r--` và checksum ở mục 1. **Không** thêm
`-u 0`: lệnh phải chạy bằng đúng user của ứng dụng thì mới phát hiện lỗi quyền đọc. Chỉ kiểm tra
hai biến trên; không xuất toàn bộ environment hoặc cấu hình Compose chứa secret.

### Chẩn đoán nhanh

| Thông báo trên giao diện | Nguyên nhân thường gặp |
|---|---|
| *Dịch vụ AI phân bổ mentor tạm thời chưa sẵn sàng* | Backend không nạp được model (sai đường dẫn, không đọc được file) hoặc Ollama không phản hồi khi đang ở chế độ `Ollama`. Log backend có dòng `The mentor ranking model could not be loaded` kèm nguyên nhân. |
| *AI chưa hoàn tất phân bổ trong thời gian cho phép* | Quá 45 giây, thường gặp ở chế độ `Ollama` trên CPU khi phải tính lại embedding. |
| Preview chạy nhưng điểm 0 | Thiếu dữ liệu: hồ sơ/CV mentor, tên/mô tả dự án của team. |

```bash
sudo docker compose --project-name ehub-staging --env-file .env.staging \
  -f docker-compose.staging.yml -f docker-compose.trained.yml \
  logs --since 30m backend | grep -A 3 'mentor ranking model'
```

Lỗi nạp model không bị lưu lại: sửa xong file thì yêu cầu kế tiếp nạp lại, không cần restart backend.

**Sự cố 02/11/2026 trên staging:** script deploy đặt `umask 077`, nên file model mới trong checkout
có quyền `0600` và image chép nguyên quyền đó. Backend (user không phải root) không đọc được model.
Từ bản sửa này Dockerfile chạy `chmod -R a+rX` trên output publish, CI build lại image với model
`0600` để kiểm tra, và script deploy có bước `verify-ai-model`.

Đăng nhập admin, bảo đảm mentor có tài khoản/hồ sơ hoạt động và tham gia kỳ đang chọn,
sau đó vào **Subject Management → Lecturers & Mentors → AI preview**. Tên/mô tả dự án,
hồ sơ từ CV và lịch sử/thành tích hiện có trong database được dùng để đề xuất.
Preview không ghi phân công; admin bấm chốt mới lưu. Điểm 0 vẫn cần xem lý do.
`/health/ready` không kiểm tra chất lượng hoặc nạp model, nên phải thử AI preview thực tế.

Script không tự dừng container Ollama đã chạy từ trước; việc đổi provider không
giải phóng RAM của container đó (tối đa 3 GB mỗi môi trường). Ở chế độ `Trained` không chức năng
nào gọi Ollama, nên có thể dừng nó mà vẫn giữ volume để rollback:

```bash
cd /opt/ehub/staging/app
sudo docker compose --project-name ehub-staging --env-file .env.staging \
  -f docker-compose.staging.yml stop ollama
```

Quay lại `Ollama`/`Hybrid` bằng cách đổi một dòng provider rồi deploy lại; script tự khởi động
Ollama và `bge-m3` vẫn còn trong volume. Không sửa database hoặc xóa volume. Model tùy chỉnh sau này có thể dùng `Mentoring__TrainedModelPath`
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
