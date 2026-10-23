# Bản thử nghiệm ghép mentor có ngữ nghĩa (Hybrid)

Ngày: 21/10/2026, PC Windows local. Hướng dẫn chạy và giới hạn chi tiết: [semantic README](../../tools/mentor-matching/semantic/README.md).

## Đã làm

- Thêm provider `Hybrid`: giữ head tự train v2 và dùng encoder đa ngôn ngữ pretrained để đối chiếu các câu dự án với các câu kinh nghiệm. Không fine-tune neural model hoặc train thêm head trong lượt này; chưa có nhãn thực tế.
- Đầu vào dự án chỉ tên đề tài + mô tả; không đổi contract/schema. Giữ quyền tài nguyên, account/profile active, nhân sự học kỳ, hai slot và optimistic concurrency của luồng phân công.
- Giữ bằng chứng nghề nghiệp, lọc liên hệ/phủ định, không dùng tên, email, ID thật hay học vấn làm lý do bổ sung ứng viên. Lý do có trích đoạn được lấy từ hồ sơ, không được AI tự viết.
- Chỉ thêm ứng viên khi đủ mức giống và cách biệt với ứng viên khác cùng slot; vẫn được phép từ chối gợi ý. Tính margin trên tất cả batch, không chỉ batch chứa ứng viên.
- Sửa trường hợp “ai sở hữu” bị coi là AI, giữ đồng bộ Python/.NET.
- Model ONNX int8 118 MB và tokenizer 9 MB được khóa revision/hash; inference chỉ chạy trên CPU local. Process thực phục vụ port 8091 đo working set khoảng 446 MB sau benchmark (không phải peak hoặc số đo VPS).
- Tạo phiếu đánh giá offline 0–3, 33 dự án/462 cặp giả lập. Không hiện score/đáp án, không điền nhãn tự động; cặp chưa chấm không trở thành 0. JSONL xuất ra có provenance và tương thích trainer v2. Chấm lại demo vẫn là nhãn demo, không chứng minh phù hợp trên dự án thật.

## So sánh thực thi

Tất cả lần chạy đi qua reader/parser/upload và recommendation handler .NET thật, EF Core InMemory và fake storage trong RAM. Service ONNX là thật; không gọi Ollama/embedding cũ, không upload CV ra Internet. Mỗi lượt đọc được 14/14 CV giả lập; private marker giả bị loại. Artifact head v2 không đổi.

| Bộ ca | v2 Top-1 khớp kỳ vọng | Hybrid Top-1 khớp kỳ vọng | Kiểm tra thêm Hybrid |
|---|---:|---:|---:|
| 12 mô tả diễn đạt mới (`semantic-projects.json`) | 6/10 vị trí có kỳ vọng | 9/10 | 3/3 |
| 12 ca hồi quy cũ | 16/16 trong báo cáo v2 trước đó | 16/16 | 5/5 |
| 10 ca kiểm tra cũ | 13/13 trong báo cáo v2 trước đó | 13/13 | 7/7 |
| 8 ca xác minh ghi sau khi khóa tham số | 4/6 | 5/6 | 5/5 |

Top-3 có cùng các tỷ số trên. Mẫu số là vị trí Business/IT có kỳ vọng tác giả khai báo, không phải tổng số dự án hoặc tất cả nhãn mentor. Những slot có danh sách kỳ vọng trống không được tự tính thành đúng; các ca từ chối có kiểm tra riêng như trong JSON.

**Không diễn giải các tỷ số thành độ chính xác thực tế.** Toàn bộ kỳ vọng do cùng tác giả tạo. Bộ 12 mô tả đã dùng để chỉnh policy, vì vậy là bộ phát triển. Bộ 8 ca xác minh không được dùng để chỉnh tiếp, nhưng vẫn không phải đánh giá độc lập. Không fine-tune model bằng hai bộ này.

Các ca còn thiếu:

- S04, mô tả quyền dùng tranh nhưng không ghi thuật ngữ pháp lý: Hybrid từ chối gợi ý vì các mức giống mơ hồ, chưa tìm được B06 theo kỳ vọng. Lỗi gợi ý IT do đại từ “ai” đã được loại.
- V01, tổ chức đưa trái cây từ vườn tới quầy bằng diễn đạt ngoài danh mục: vẫn bỏ sót mentor B03. Không giảm ngưỡng để làm cho ca này đạt sau khi xem kết quả.
- Mô tả quá chung hoặc đòi hỏi công nghệ sinh học ngoài bộ hồ sơ không tạo thêm ứng viên trong các ca đã thử. Chưa chứng minh có thể từ chối mọi ca ngoài chuyên môn.
- Chưa đo tải/concurrency trên VPS hoặc mentoring outcome thực. Công thức blend 75/25 và threshold/margin là policy thử nghiệm; cosine/điểm hiển thị không phải xác suất thành công.

Kết quả đầy đủ, kèm fingerprint input/source và revision encoder:

- [v2 trên bộ phát triển](../../tools/mentor-matching/demo/semantic-results.json)
- [Hybrid trên bộ phát triển](../../tools/mentor-matching/demo/hybrid-semantic-results.json)
- [Hybrid hồi quy](../../tools/mentor-matching/demo/hybrid-results.json)
- [Hybrid bộ kiểm tra cũ](../../tools/mentor-matching/demo/hybrid-holdout-results.json)
- [v2 trên 8 ca xác minh](../../tools/mentor-matching/demo/semantic-verification-results.json)
- [Hybrid trên 8 ca xác minh](../../tools/mentor-matching/demo/hybrid-semantic-verification-results.json)

## Kiểm thử phần mềm đã chạy

| Lệnh/nhóm | Kết quả thật |
|---|---|
| `dotnet build EHub.slnx --no-restore -m:1 /nr:false` | Đạt, 0 lỗi; còn warning dependency PKCS và nullable/deprecated đã có |
| `dotnet test EHub.slnx --no-build --no-restore --filter FullyQualifiedName!~IntegrationTests` | ApplicationTests 558/558 và UnitTests 141/141 tại thời điểm chạy |
| Chạy lại toàn bộ ApplicationTests sau 3 test bổ sung | 561/561 đạt |
| Chạy riêng các test semantic/recommendation cuối | 40/40 đạt, gồm hủy request, ẩn danh, sai revision, malformed response, phủ định/slot và margin qua nhiều batch |
| Trainer Python | 13/13 đạt |
| Semantic protocol/review Python | 8/8 đạt |
| `npm run lint` | Đạt, còn warning có sẵn |
| `npm run type-check` | Đạt |
| `npm test` | Đạt |
| `npm run build` | Đạt |
| `git diff --check` | Đạt |
| UI phiếu đánh giá | Đã thử chọn/xóa nhãn, chuyển cặp và chặn xuất khi chưa chấm; kết thúc có 0 nhãn. Không dùng thao tác test này làm dữ liệu train |

Integration suite đã thử hai lượt: trước và sau khi khởi động Docker Desktop/đặt `DOCKER_HOST=npipe://./pipe/dockerDesktopLinuxEngine`. Cả hai bị lỗi khởi tạo Testcontainers vì Docker engine không phản hồi. Lượt cuối: **422 failed do Docker unavailable, 15 passed, 1 skipped, tổng 438**. Không báo suite đạt. Test opt-in `HybridProviderUsesRealLocalOnnxAndUploadedExperienceThroughTheApi` đã được viết/build nhưng chưa chạy được vì fixture PostgreSQL không khởi tạo. Ca API 401/403 cho Hybrid đã có trong theory, cũng bị chặn cùng fixture.

Lệnh cần chạy lại khi Docker sẵn sàng:

```powershell
# Bật semantic/server.py trước; chạy từ backend/
$env:EHUB_TEST_LOCAL_SEMANTIC='1'
dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj
```

## Bước tiếp theo để kiểm chứng thực tế

Giữ provider mới ở chế độ thử nghiệm. Thu snapshot tên/mô tả dự án và chuyên môn đã ẩn danh được phép dùng, lấy đánh giá 0–3 từ người hiểu nghiệp vụ. Phiếu hỗ trợ import JSONL local qua `review.py --data`, không tự xuất dữ liệu production. Giữ riêng các nhóm dự án để train/validation/test; xem cả gợi ý sai, bỏ sót và các cặp bị từ chối trước khi thay head/ngưỡng hoặc bật production. Không đánh dấu model là đã kiểm chứng chỉ vì test code hoặc demo đạt.
