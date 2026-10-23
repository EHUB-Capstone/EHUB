# Kiểm tra bản thử nghiệm model ghép mentor

> Báo cáo lịch sử cho bản v1 trước khi cải thiện phủ định và chuyển sang đầu vào chỉ tên đề tài + mô tả. Kết quả hiện tại xem [báo cáo v2](MENTOR_RANKER_V2_REPORT.md) và [demo v2](../../tools/mentor-matching/demo/README.md). Các số liệu bên dưới không phải kết quả chạy lại bản v2.

Ngày kiểm tra: 19/10/2026, trên PC Windows local. Bản thử nghiệm có trainer Python và model xếp hạng tuyến tính chạy CPU trong backend .NET. Provider mới được bật bằng `Mentoring:MatchingProvider=Trained`; mặc định vẫn là Ollama. Chưa triển khai hoặc đo tải trên VPS.

## Kết quả và phạm vi

| Kiểm tra | Kết quả |
|---|---|
| Build solution backend | Đạt; còn các cảnh báo đã có ở RegisterCommandHandlerTests, CustomWebApplicationFactory và MentorImportIntegrationTests |
| ApplicationTests | 511 đạt, không lỗi |
| UnitTests | 141 đạt, không lỗi |
| Trainer Python | 7 đạt, không lỗi |
| IntegrationTests | 431 đạt, 0 lỗi, 1 skipped trên tổng 432; khoảng 1 phút 32 giây |
| git diff --check | Đạt |
| Frontend | Không thay đổi frontend; không chạy các lệnh frontend |

Các test mới kiểm tra kỹ năng chuyên môn từ form, giữ Skills nhập tay và loại mentor, lọc phần cá nhân, thống nhất đặc trưng Python/.NET, kinh nghiệm so với Bio, tên kỹ năng có ký hiệu, quyền truy cập, nhân sự học kỳ, vị trí mentor tạm, provider Trained không gọi Ollama, và lỗi model không lộ đường dẫn.

Ca skipped là `LiveBgeM3ReceivesExtractedExperienceAndStoresVector`: chưa bật opt-in `EHUB_TEST_REAL_OLLAMA=1`, nên không kiểm thử BGE-M3 thật ở lượt này. Các ca provider Trained mới, bao gồm HTTP 200/401/403/503, đều đã thực thi và đạt. Không thay test model thật thành mock để báo đạt.

Application chỉ dùng Domain/Contracts/Shared và các abstraction hiện có. Loader file/model nằm ở Infrastructure. Không đổi schema, API response hoặc luồng phân công/Outbox.

## Lệnh đã chạy

Từ `backend/`:

```powershell
dotnet build EHub.slnx --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal

$env:DOTNET_Thread_DefaultStackSize = '800000'
dotnet test EHub.slnx --no-build --no-restore --filter 'FullyQualifiedName!~IntegrationTests' -m:1 /nr:false -v:minimal -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1

dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build --no-restore -m:1 /nr:false -v:minimal -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1
```

Lượt build song song mặc định ban đầu không hoàn tất trong sandbox; lượt test ban đầu gặp lỗi kết nối testhost và tràn stack Castle. Các kết quả đạt ở trên thuộc lượt dùng một luồng, stack 8 MiB cho thread .NET và testhost ngoài sandbox. Không thay cấu hình runtime của sản phẩm để chạy test. Hai test API mới ban đầu lỗi do WebApplicationFactory áp dụng cấu hình sau bước đăng ký service; fixture đã được sửa, kèm test riêng cho lựa chọn provider thực tế trong AddInfrastructure.

Từ thư mục gốc repo, dùng Python bundled của Codex:

```powershell
python -m unittest discover -s tools/mentor-matching -p 'test_*.py' -v
python tools/mentor-matching/train.py --bootstrap --output backend/src/EHub.Infrastructure/Mentoring/Models/bootstrap-model.json --parity-output tools/mentor-matching/parity-fixtures.json
```

## Huấn luyện và giới hạn chất lượng

Đã train thực sự trên curriculum hoàn toàn giả lập: 30 tình huống startup, 372 cặp ứng viên, chia theo projectId thành train 18 nhóm/223 dòng, validation 6 nhóm/74 dòng, test 6 nhóm/75 dòng. Giữ nguyên toàn bộ projectId trong cùng tập; không chia ngẫu nhiên từng cặp. Seed 17; validation chọn epoch 1. Artifact JSON khoảng 8 KiB và không chứa hồ sơ nguồn.

Trên test giả lập, NDCG@5 là 1,0 và Recall@3 là 0,875, với relevance từ 2 trở lên. **Đây không phải độ chính xác 100% hoặc 87,5% trên startup thực tế.** Nó chỉ đo khả năng tái hiện nhãn của tình huống mô phỏng. Bộ test vẫn dùng cùng danh mục chủ đề với bộ train. Model không hiểu ngữ cảnh/phủ định như encoder ngôn ngữ và chưa được so sánh với BGE-M3/công thức 70/30 trên dữ liệu thực tế.

Phép đo dùng điểm nguyên giống thang hiển thị; hòa điểm được xếp theo mentorRef giả, trong khi API xếp theo tên hiển thị. Điểm 0–100 là điểm xếp hạng, không phải xác suất mentoring thành công. Metadata `suitabilityValidated` vẫn false.

## Kiểm tra file form do người dùng cung cấp

Đọc trực tiếp file DOCX mẫu tại đường dẫn người dùng chỉ định bằng reader/parser thật, không sao chép file hoặc nội dung cá nhân vào repo/dataset/fixture. Probe cục bộ được giữ trong thư mục artifacts đã được Git bỏ qua; không upload tài liệu.

Reader nhận diện thành công các mục nghề nghiệp, tạo Bio dài 734 ký tự và Experience dài 608 ký tự. Kiểm tra email/URL trên văn bản chuyên môn trả false. Khi đối chiếu hai nhu cầu giả lập, hồ sơ được xếp cao hơn cho marketing/kinh doanh/vận hành so với backend C#/PostgreSQL. Đây chỉ là một ca kiểm tra hành vi trên một hồ sơ, không phải phép đánh giá độ chính xác tổng quát hay kiểm định loại bỏ toàn bộ thông tin cá nhân.

Hướng dẫn train lại, bật chế độ mới, dùng artifact riêng và quay về provider cũ: [tools/mentor-matching/README.md](../../tools/mentor-matching/README.md).
