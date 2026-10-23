# Model xếp hạng mentor cho dự án khởi nghiệp

Provider ngữ nghĩa `Hybrid` và phiếu đánh giá để thu nhãn đã được bổ sung trong [semantic/README.md](semantic/README.md). Đây là lớp thử nghiệm dùng encoder tiếng Việt có sẵn kết hợp head v2; chưa phải encoder tự train bằng nhãn E-HUB.

Thư mục này cung cấp một mô hình xếp hạng nhỏ có thể tự train trên PC và chạy trực tiếp bằng CPU trong backend .NET. Chế độ `Trained` không gọi Ollama và không cần Python trên VPS. Backend vẫn kiểm tra quyền, nhân sự học kỳ, trạng thái mentor và vị trí phân công trước khi gọi model. Model chỉ gợi ý; người phụ trách quyết định phân công.

**Đây là bản khởi đầu để thử nghiệm, chưa chứng minh ghép mentor chính xác hơn hệ thống hiện tại.** Model đi kèm được train trên tình huống khởi nghiệp hoàn toàn giả lập. Nó học trọng số của các tín hiệu từ khóa/chủ đề và nguồn bằng chứng; không phải mô hình ngôn ngữ hay embedding hiểu ngữ cảnh. Chưa xử lý được đầy đủ phủ định, năng lực cố vấn thực tế hoặc nhu cầu được diễn đạt ngoài danh mục chủ đề. Không dùng điểm này làm xác suất thành công.

## Đầu vào từ form mentor

Đầu vào dự án chỉ gồm **tên đề tài và mô tả**: ưu tiên Project.Name/Description, dùng TeamName/Description khi chưa có nội dung tương ứng. Không yêu cầu form nhu cầu Business/IT riêng; không đưa Technology, StartupField, Problem, Solution hoặc tag dự án vào model. Thiếu mô tả trả validation; mô tả có văn bản nhưng không đủ bằng chứng khớp có thể trả danh sách rỗng.

File DOCX/PDF được đọc tại backend. Các mục kinh nghiệm, học vấn, kỹ năng chuyên môn và nội dung nghề nghiệp được giữ để tạo hồ sơ. Thông tin cá nhân, người thân, liên hệ, sở thích và chữ ký bị loại khỏi phần trích xuất. Không tự thay loại mentor, công ty, số năm hay ghi đè Skills nhập tay. Mục kỹ năng chuyên môn được giữ trong Bio; mục Vi tính/Computer skills thông thường vẫn bị loại.

Mô hình dùng riêng các nguồn bằng chứng sau: kinh nghiệm làm việc, chuyên môn/tag, lĩnh vực khởi nghiệp, kỹ năng đã khai báo, và giới thiệu/học vấn. Việc học về tài chính trong Bio không được tự biến thành kinh nghiệm tài chính. Các trọng số là giá trị học từ dữ liệu, không phải tỷ lệ 70/30 cố định. Mỗi tín hiệu bằng chứng có hệ số không âm; thêm bằng chứng phù hợp không làm giảm điểm khi các tín hiệu khác giữ nguyên.

Schema v2 lọc phủ định rõ ràng tiếng Việt/Anh trước khi tìm chuyên môn; giữ cấu trúc “không chỉ… mà còn…”. Tag cũ không được lấn át câu phủ định trong CV. Khi tải CV mới, học vấn vẫn nằm trong Bio nhưng không tự sinh tag chuyên môn. Không tự xóa tag đã lưu vì chưa có thông tin phân biệt tag nhập tay và tag được suy ra trước đây.

Bộ đọc CV phân biệt nhãn liên hệ với câu nghề nghiệp: “Điện thoại: …” bị loại, còn “phát triển ứng dụng trên điện thoại” được giữ làm bằng chứng mobile. Tương tự với email marketing và kinh nghiệm ngân hàng; số điện thoại/email/URL thực trong văn bản chuyên môn vẫn được xử lý bởi bộ lọc liên hệ hiện có.

Danh mục có vai trò Business/IT, lĩnh vực và quan hệ chuyên môn cụ thể–chủ đề cha, ví dụ logistics/vận hành, .NET/backend. Loại mentor chỉ chọn các chủ đề yêu cầu tương ứng, không tự đổi vai trò. Chỉ trùng từ chung hoặc chỉ trùng ngành khi dự án không có nhu cầu IT sẽ không tạo gợi ý IT. Ứng viên khớp một phần có lý do ghi rõ chuyên môn chưa có bằng chứng. Xếp hạng dùng điểm chưa làm tròn; điểm hiển thị không phải xác suất.

Các trường riêng về tên, email, ID, tổ chức, tuổi, số năm và số nhóm đang phụ trách không được đưa vào model. Văn bản giới thiệu/kinh nghiệm tự nhập vẫn có thể chứa tên tổ chức, mốc thời gian hoặc thông tin cá nhân; cần kiểm tra và khử thông tin cá nhân trước khi dùng để train lại. Mentor IT và Business vẫn thuộc các slot hiện tại; model không tự đổi loại. Số nhóm đang phụ trách tiếp tục chỉ mang tính tham khảo.

## Train trên PC

Trainer chỉ dùng thư viện chuẩn Python 3.10 trở lên, không cần GPU hay tải model. Chạy từ thư mục gốc repo:

```powershell
python tools/mentor-matching/train.py --bootstrap --output artifacts/mentor-matching/model.json
python -m unittest discover -s tools/mentor-matching -p "test_*.py" -v
```

`--bootstrap` tự tạo các cặp startup–mentor từ `curriculum.json` và `topic-catalog.json`. Không đọc CV thật, database, mạng hay file cấu hình riêng. Nhãn 0–3 là giả định của tình huống mô phỏng, không phải đánh giá giảng viên. Kết quả gồm model JSON và báo cáo `.report.json`.

Huấn luyện kết hợp so sánh cặp ứng viên trong cùng dự án/slot với lỗi dự đoán trên nhãn mô phỏng. Chọn epoch bằng validation, đánh giá test sau khi chọn model. Toàn bộ một projectId, kể cả các slot, nằm trong cùng một tập; không chia ngẫu nhiên từng cặp. Nên dùng cùng projectId cho mọi phiên bản gần trùng của dự án để tránh rò rỉ.

`backend/src/EHub.Infrastructure/Mentoring/Models/bootstrap-model.json` là bản đã train và đóng gói trong assembly Infrastructure, nằm trong Docker build context backend. Để tạo lại bản này và fixture kiểm tra sự thống nhất giữa Python/.NET:

```powershell
python tools/mentor-matching/train.py --bootstrap --output backend/src/EHub.Infrastructure/Mentoring/Models/bootstrap-model.json --parity-output tools/mentor-matching/parity-fixtures.json
```

Seed mặc định là 17. Cùng dữ liệu/seed sinh cùng hệ số; thời gian trong báo cáo có thể khác. Không sao chép dữ liệu từ form thật vào curriculum hoặc fixture Git.

## Chạy trong E-HUB

Mặc định vẫn là provider `Ollama` để tránh tự thay hành vi môi trường đang chạy. Để thử chế độ đã train khi chạy backend local:

```powershell
$env:Mentoring__MatchingProvider = 'Trained'
dotnet run --project backend/src/EHub.Api
```

Nếu chưa đặt `Mentoring__TrainedModelPath`, backend dùng bootstrap model nhúng sẵn. Để dùng artifact đã train riêng, đặt đường dẫn trên máy/container chạy backend:

```powershell
$env:Mentoring__TrainedModelPath = 'E:\DO AN\EHUB\artifacts\mentor-matching\model.json'
```

Với Docker, thêm các biến trên vào `environment` của service backend hoặc file compose override; biến môi trường trên host không tự đi vào container. Model riêng cần volume mount chỉ đọc và đường dẫn trong container. Chế độ Trained không cần Ollama để trả gợi ý, nhưng cấu hình compose hiện tại vẫn có service Ollama; tài nguyên container đó không tự được giải phóng chỉ bằng việc đổi provider.

Loader kiểm tra phiên bản, thứ tự đặc trưng, kích thước tối đa 256 KiB, chủ đề và hệ số hợp lệ. File lỗi/không tồn tại trả lỗi gợi ý an toàn, không tự chuyển provider hoặc lộ đường dẫn. Model được nạp một lần; khởi động lại backend khi thay artifact. Khôi phục provider `Ollama` và khởi động lại để quay về cách cũ. Không cần migration hoặc thay API/frontend.

Artifact hiện dùng `ehub-mentor-fit-v2`. Model v1 riêng không tương thích với cách trích bằng chứng mới: train lại bằng trainer hiện tại hoặc dùng model v2 nhúng sẵn. Không đổi tên schema trong file v1 để bỏ qua kiểm tra, vì hệ số được học với cách xử lý đầu vào khác.

## Kiểm tra chất lượng

Bộ [demo tên đề tài + mô tả](demo/README.md) có 14 CV hư cấu, 12 tình huống hồi quy và 10 mô tả mới ngoài curriculum. Báo cáo giữ lỗi/thứ hạng thực tế, có baseline v1 và hash đầu vào/model/mã nguồn. Trainer chỉ đọc curriculum hoặc JSONL được chỉ định, không đọc bộ demo. Các kỳ vọng demo vẫn do tác giả đặt ra, không phải đánh giá độc lập.

Báo cáo có NDCG@5 và Recall@3; relevance từ 2 trở lên được coi là phù hợp trong phép đo. Các chỉ số trên bootstrap chỉ đo mức tái hiện tình huống giả lập. Không diễn giải chúng thành phần trăm độ chính xác trên dự án thật. Chưa có phép so sánh với BGE-M3/công thức 70/30 trên cùng dữ liệu thực tế.

Trước khi dùng làm mặc định, cần so sánh trên những startup chưa dùng để train, với cùng danh sách ứng viên đủ điều kiện. Phản hồi "mentor phù hợp/chưa phù hợp" có thể được thu dần khi sử dụng; không cần chuẩn bị bảng chấm điểm lớn để chạy thử. Mentor chưa được chọn không tự là mẫu âm. Với mentor hoàn toàn mới, nên có phép thử riêng để tránh chỉ đo khả năng nhớ các hồ sơ đã biết.

Đo thêm RAM và độ trễ khi backend/PostgreSQL cùng hoạt động trên VPS. Kết quả chạy PC không bảo đảm tốc độ VPS. Dữ liệu không có bằng chứng khớp nhận điểm 0 thay vì tạo mức chắc chắn giả.

## Train lại với dữ liệu cục bộ khi có

Lưu file JSONL đã khử thông tin cá nhân trong `tools/mentor-matching/local/` (được Git bỏ qua), rồi chạy `--data <file>` thay cho `--bootstrap`. Mỗi dòng có cấu trúc:

```json
{"projectId":"anonymous-project-1","slot":"Business","projectText":"Nhu cầu cố vấn marketing","mentorRef":"anonymous-mentor-1","mentor":{"expertise":["Marketing"],"startupDomains":[],"technologySkills":[],"tags":[],"bio":"","experience":"Đã phụ trách marketing","experiences":[]},"relevance":2,"labelSource":"local-review","isSynthetic":false}
```

Đây là ví dụ giả về định dạng. Cần ít nhất 5 projectId để chia ba tập, không phải ngưỡng bảo đảm chất lượng. Không chuyển giá trị chưa biết thành 0. Không đưa CV gốc, liên hệ, thông tin người thân hoặc dữ liệu production chứa thông tin cá nhân vào Git hay chat. Trainer không gửi dữ liệu ra ngoài; artifact chỉ chứa danh mục chủ đề/hệ số và metadata, không chứa hồ sơ nguồn. Metadata `suitabilityValidated` vẫn false; phép đo có nhãn cục bộ chưa tự chứng minh hiệu quả thực tế.

Tài liệu phương pháp: [Pairwise learning to rank](https://catboost.ai/docs/en/concepts/loss-functions-ranking), [đánh giá thứ hạng](https://www.sbert.net/docs/package_reference/cross_encoder/evaluation.html). Trainer trong repo là triển khai tuyến tính nhỏ bằng Python, không sử dụng CatBoost.
