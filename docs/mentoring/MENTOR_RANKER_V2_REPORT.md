# Kiểm tra cải thiện ghép mentor v2

Ngày: 19/10/2026. Đầu vào dự án chỉ gồm tên đề tài và mô tả. Bộ đọc CV dùng dữ liệu nghề nghiệp trong file mentor; không cần form nhu cầu Business/IT riêng.

## Kết quả chức năng

- Giữ 16/16 vị trí đứng đầu khớp kỳ vọng tác giả ở 8 dự án thông thường của demo cũ.
- Các kiểm tra ca khó tăng từ 2/5 lên 5/5: loại ứng viên không có chuyên môn khớp, loại bằng chứng AI bị phủ định, ưu tiên đúng nhu cầu .NET, phân biệt bằng cấp với kinh nghiệm và giữ validation khi thiếu mô tả.
- 10 mô tả mới ngoài curriculum: 13/13 vị trí có kỳ vọng khớp Top 1, 7/7 kiểm tra bổ sung đạt. Có hai mô tả tiếng Việt thông thường không nêu framework/kỹ năng tiếng Anh, và các mô tả không cần IT hoặc quá chung chung.
- Mỗi bộ đọc thành công 14/14 CV giả lập. Không gọi embedding/Ollama. Model giữ nguyên trong các lần đánh giá.
- Sau sửa bộ đọc CV, cả 14/14 mục kinh nghiệm làm việc được giữ ở cả hai bộ chạy; marker riêng tư giả vẫn bị loại.
- Lý do ghép ghi cả bằng chứng khớp và phần chuyên môn chưa có bằng chứng. Ví dụ mentor biết mobile không tự được mô tả là chuyên gia React Native.
- Sửa bộ đọc CV để không loại nhầm đoạn kinh nghiệm chỉ vì có từ điện thoại, email hoặc ngân hàng. Các nhãn liên hệ riêng tư vẫn bị loại; có test đối chiếu cả hai trường hợp.

Toàn bộ danh sách, mô tả, điểm và lý do: [demo v2](../../tools/mentor-matching/demo/README.md). Dữ liệu/kết quả v1 giữ trong `tools/mentor-matching/demo/baseline/`. Các đoạn nhu cầu ở 12 ca cũ được gộp vào mô tả, giữ nguyên nội dung để kiểm tra hồi quy; các nhãn kỳ vọng không đi vào model.

**Các kết quả trên là kiểm tra giả lập do tác giả thiết kế, không phải độ chính xác thực tế hay kiểm định độc lập.** Trainer không đọc file demo, nhưng bộ kiểm tra mới cũng do cùng tác giả tạo nên không được gọi là thử nghiệm mù.

## Kiểm thử phần mềm

| Kiểm tra | Kết quả |
|---|---|
| Build toàn bộ backend | Đạt, 0 lỗi; lượt build cuối còn 1 cảnh báo CS8602 đã có ở RegisterCommandHandlerTests |
| ApplicationTests | 537 đạt, 0 lỗi |
| UnitTests | 141 đạt, 0 lỗi |
| Trainer Python | 12 đạt, 0 lỗi |
| IntegrationTests | 432 đạt, 0 lỗi, 1 skipped trên tổng 433; lượt cuối khoảng 1 phút 36 giây |
| Demo | Build 0 lỗi/0 cảnh báo; 12 ca hồi quy + 10 mô tả mới chạy thành công |
| git diff --check | Đạt |
| Frontend | Không sửa frontend; không chạy lại bộ test frontend |

Test mới bao phủ phủ định tiếng Việt/Anh, cấu trúc “không chỉ… mà còn…”, câu phủ định lấn át từ trong tên đề tài và tag CV cũ, React Native khác React web, cảnh báo khớp một phần, nguồn bằng chứng học vấn, đầu vào chỉ tên/mô tả, danh sách rỗng khi thiếu bằng chứng, và sắp hạng trước khi làm tròn. Kiểm tra parity đối chiếu các vector Python/.NET, gồm cả các trường hợp phủ định và vai trò.

Ca BGE-M3 thật vẫn là opt-in; chưa bật `EHUB_TEST_REAL_OLLAMA=1`. Không dùng kết quả provider Trained để tuyên bố đã kiểm tra BGE-M3 thật.

Các lệnh từ `backend/`:

```powershell
dotnet build EHub.slnx --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -v:minimal
$env:DOTNET_Thread_DefaultStackSize = '800000'
dotnet test EHub.slnx --no-build --no-restore --filter 'FullyQualifiedName!~IntegrationTests' -m:1 /nr:false -v:minimal -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1
dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build --no-restore -m:1 /nr:false -v:minimal -- xUnit.ParallelizeTestCollections=false xUnit.MaxParallelThreads=1
```

Testhost chạy ngoài sandbox để dùng IPC và PostgreSQL Testcontainers cục bộ. Stack 8 MiB và một luồng chỉ áp dụng cho tiến trình test; không đổi cấu hình runtime sản phẩm. Các kỳ vọng test trước đây tự suy tag từ bằng cấp đã được cập nhật để kiểm tra ngược lại, đồng thời vẫn kiểm tra giữ nội dung Bio và kỹ năng nhập tay.

## Model và huấn luyện

Model tuyến tính chạy CPU, với bộ lọc văn bản theo quy tắc và danh mục 38 chủ đề. Có 8 đặc trưng bằng chứng; vai trò mentor chỉ chọn các chủ đề nhu cầu tương ứng. Các chủ đề cụ thể như logistics, .NET, PostgreSQL và React Native có quan hệ với chủ đề cha. Bằng chứng chỉ trùng từ chung không đủ để sinh gợi ý.

Đã train trên 40 nhóm tình huống hoàn toàn giả lập, 813 dòng: train 24 nhóm/485 dòng, validation 8 nhóm/164 dòng, test 8 nhóm/164 dòng. Các phiên bản/cặp của cùng projectId không tách sang tập khác. Seed 17, validation chọn epoch 1. Artifact 13.153 byte, schema `ehub-mentor-fit-v2`.

Test curriculum: NDCG@5 = 0,9650378964; Recall@3 = 0,80625. Nhãn 0–3 là giả định mô phỏng, không phải đánh giá giảng viên. Không so trực tiếp các số này với v1 vì curriculum và tập ứng viên đã thay đổi. Không diễn giải thành tỷ lệ ghép thành công thực tế. Metadata `suitabilityValidated` vẫn false.

Lệnh từ gốc repo:

```powershell
python tools/mentor-matching/train.py --bootstrap --output backend/src/EHub.Infrastructure/Mentoring/Models/bootstrap-model.json --parity-output tools/mentor-matching/parity-fixtures.json
python -m unittest discover -s tools/mentor-matching -p 'test_*.py' -v
```

Demo có lệnh chạy lại trong README riêng. JSON kết quả ghi hash đầu vào/model, các file triển khai liên quan và assembly đã nạp; tạo lại báo cáo sẽ đối chiếu những hash nguồn đã lưu.

## Giới hạn và vận hành

Chưa hiểu đầy đủ ngữ cảnh nhiều câu, mọi cách phủ định, mức độ ưu tiên, ràng buộc bắt buộc hoặc chuyên môn ngoài danh mục. Có thể bỏ sót mô tả diễn đạt khác; ứng viên khớp một phần vẫn có thể xuất hiện và được nêu phần thiếu trong lý do. Cần đánh giá bằng phản hồi ghép thật để biết chất lượng ngoài bộ mẫu. Chưa có so sánh với BGE-M3, đo tải VPS hoặc kiểm định khử toàn bộ thông tin cá nhân.

Không đổi schema database, API response, frontend hoặc quyền phân công. Default provider vẫn là Ollama; bật bản tự train bằng `Mentoring__MatchingProvider=Trained`. Model v1 riêng phải được train lại bằng trainer hiện tại; không sửa tay tên schema để né kiểm tra. Loader cache model một lần, nên cần khởi động lại backend sau khi thay artifact. Chưa triển khai lên VPS.
