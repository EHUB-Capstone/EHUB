# Quy trình nhánh và phát hành EHUB

Tài liệu này mô tả nhánh nào được deploy lên môi trường nào. Các bước thao tác
chi tiết trên VPS nằm trong `STAGING_VPS.md`, `PRODUCTION_VPS.md` và quy trình
deploy nhanh của nhóm.

## 1. Vai trò của từng nhánh

| Nhánh | Vai trò | Môi trường |
| --- | --- | --- |
| `feature/*`, `fix/*` | Công việc của từng thành viên | Không deploy |
| `develop` | Nhánh tích hợp, nhánh mặc định, nhận PR của thành viên | **Staging** (commit mới nhất hoặc bất kỳ commit nào trên `develop`) |
| `main` | Chỉ chứa mã đã nghiệm thu, nhận release PR từ `develop` | **Production** |

## 2. Luồng phát hành

```text
feature/* --PR--> develop --tu dong (workflow Deploy Staging)--> STAGING --nghiem thu--+
                                                                                       |
                  release PR (merge commit) --> main <---------------------------------+
                                                 |
                       bam tay (workflow Deploy Production)
                                                 v
                                            PRODUCTION
```

1. Thành viên mở PR vào `develop`; CI phải đạt và có người duyệt.
2. Staging tự deploy khi PR được merge vào `develop` (`STAGING_CD.md`). Có thể đóng băng
   bằng biến `STAGING_AUTO_DEPLOY` khi đang nghiệm thu, hoặc deploy tay theo `STAGING_VPS.md`.
3. Người nghiệm thu (cô Sương) kiểm tra trên staging. Ghi lại **SHA đã nghiệm thu**.
4. Tạo release PR vào `main` **từ đúng SHA đã nghiệm thu** (mục 3).
5. Sau khi release PR được duyệt và merge, chạy workflow **Deploy Production** với SHA mới
   nhất của `main` (`PRODUCTION_CD.md`), hoặc deploy tay theo `PRODUCTION_VPS.md`.
6. Cổng kiểm tra trên VPS: SHA thuộc `origin/main` **và** nội dung (tree) trùng một bản đã
   deploy thành công lên staging (mục 4).

## 3. Tạo release PR

Dùng đúng commit đã nghiệm thu, không dùng đỉnh `develop` nếu `develop` đã có thêm commit mới.

```powershell
git fetch origin
git push origin <SHA_DA_NGHIEM_THU>:refs/heads/release/<YYYYMMDD>
```

Trên GitHub: mở PR với **base = `main`**, **compare = `release/<YYYYMMDD>`**.
Phương thức merge bắt buộc là **Create a merge commit**. Không dùng Squash hay
Rebase, vì hai cách này làm lịch sử `main` và `develop` phân kỳ và phá cổng kiểm tra
của mục 4. Sau khi merge có thể xóa nhánh `release/*`.

## 4. Cổng kiểm tra production: so sánh nội dung, không so sánh SHA

Commit merge vào `main` có SHA khác SHA đã test trên staging dù nội dung giống hệt.
Vì vậy cổng so sánh **tree** của Git (mã băm toàn bộ nội dung file của commit):

```bash
test "$(git rev-parse "$EHUB_RELEASE_SHA^{tree}")" = "$(git -C /opt/ehub/staging/app rev-parse "$EHUB_STAGING_SHA^{tree}")"
```

Nếu `main` có thêm thay đổi chưa chạy trên staging (ví dụ hotfix) thì tree khác và
cổng chặn deploy. Đây là hành vi mong muốn.

Script `deploy-production.sh` so tree của bản release với **mọi commit đã deploy thành
công lên staging** (đọc từ `/var/log/ehub-staging-deploy.log`, cộng commit staging đang
chạy), không chỉ với bản staging đang chạy. Vì staging tự cập nhật theo `develop`, so với
bản đang chạy sẽ chặn nhầm khi `develop` đã có commit mới hơn bản nghiệm thu.

## 5. Hotfix

1. Tạo nhánh từ `main`, sửa, mở PR vào `main` (CI và người duyệt như thường).
2. Deploy lên production theo quy trình hotfix: staging phải được cập nhật cùng nội dung hoặc
   người phát hành ghi rõ lý do bỏ qua cổng ở mục 4 (workflow Deploy Production có ô
   `skip_staging_gate` cho đúng trường hợp này; Discord ghi lại việc đã bỏ cổng).
3. Ngay sau đó mở PR **`main` vào `develop`** để `develop` có cùng bản sửa. Không bỏ bước này, nếu không bản sửa mất ở lần release sau.

## 6. Rollback

Deploy lại SHA trước đó của `main` (đã ghi ở bước "SHA bản trước" khi release; workflow
Deploy Production ghi SHA này trong Discord và trong `/var/log/ehub-production-deploy.log`).
SHA này vẫn thuộc `origin/main` nên cổng thuộc nhánh vẫn đạt; **bỏ cổng so sánh với
staging** (ô `skip_staging_gate`) nếu bản đó chưa từng chạy trên staging qua script. Nếu bản lỗi đã chạy migration không tương thích, xem
mục Release and rollback trong `PRODUCTION_VPS.md` (không chạy bản cũ lên schema mới).

## 7. Cài đặt GitHub cần có cho `main`

| Cài đặt | Giá trị |
| --- | --- |
| Require a pull request before merging | Bật, ít nhất 1 người duyệt |
| Require status checks to pass | Bật, chọn **Build and Test Backend** và **Type Check and Build Frontend** |
| Block force pushes | Bật |
| Restrict deletions | Bật |
| Bypass list | Để trống |

Không đặt các check của `production-container-ci` là bắt buộc: workflow đó chỉ chạy khi
PR chạm các đường dẫn triển khai, nên check bắt buộc sẽ làm treo PR không chạm chúng.

## 8. Nhật ký phát hành

Ghi mỗi lần deploy production để có điểm rollback và dấu vết kiểm toán.

| Ngày | SHA trên `main` | SHA nghiệm thu trên staging | SHA bản trước | Người thực hiện | Ghi chú |
| --- | --- | --- | --- | --- | --- |
| 16/10/2026 | d9779fd69081be1062350d655e19677ba4b6707b | 823a6a2ee363b2e0d3013ecd60d45bbcd3959b61 | eb8bc5e2f37be5cc0a47da3644e30c0fd26f3cda | Quản trị viên phụ trách deploy | Release đầu tiên theo mô hình main/develop: health check Ollama và backup tự động lên R2 |
