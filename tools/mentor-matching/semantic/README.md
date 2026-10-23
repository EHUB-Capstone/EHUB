# Ghép mentor có đối chiếu ngữ nghĩa (Hybrid, thử nghiệm v3)

Provider `Hybrid` bổ sung encoder đa ngôn ngữ vào ranker tự train v2. Dự án vẫn chỉ có tên đề tài và mô tả. Không thay contract API, schema database, quyền phân công hay hai slot mentor hiện có.

Encoder **đã được tác giả bên ngoài huấn luyện sẵn**, chưa fine-tune bằng dữ liệu E-HUB: [`sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`](https://huggingface.co/sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2), Apache-2.0, hỗ trợ tiếng Việt. Model/revision khóa tại `e8f8c211226b894fcb81acc59f3b34ba3efd5f42`; file ONNX int8 AVX2 118,453,870 byte, tokenizer 9,081,518 byte. Setup và runtime đều xác minh SHA-256; không dùng `trust_remote_code`, PyTorch, GPU hay API AI trả phí.

## Chạy local

Từ thư mục gốc repo, dùng Python 3.12:

```powershell
python -m venv tools/mentor-matching/local/semantic-venv
$python = (Resolve-Path tools/mentor-matching/local/semantic-venv/Scripts/python.exe).Path
& $python -m pip install -r tools/mentor-matching/semantic/requirements.txt
& $python tools/mentor-matching/semantic/prepare_model.py --output artifacts/mentor-semantic/model
& $python tools/mentor-matching/semantic/server.py --model-dir artifacts/mentor-semantic/model --threads 2
```

Trong terminal backend khác, với database/config local đã có:

```powershell
$env:Mentoring__MatchingProvider = 'Hybrid'
dotnet run --project backend/src/EHub.Api
```

Service chỉ bind `127.0.0.1:8091`; backend chỉ chấp nhận loopback HTTP origin, không redirect/proxy. Không đưa port 8091 ra Internet. Health: `http://127.0.0.1:8091/health`. Suy luận chỉ đọc artifact local, không tải model theo request. CV, tên, email, ID tài khoản và tổ chức không được gửi sang nhà cung cấp model; giữa backend và service chỉ gửi số thứ tự tạm, các câu dự án và câu kinh nghiệm đã lọc liên hệ/phủ định. Cache văn bản/vector chỉ ở RAM, tối đa 512 câu; không log body.

Trên Linux/VPS: tạo venv bằng `python3 -m venv ...`, dùng đường dẫn `bin/python`. Bản ONNX AVX2 yêu cầu CPU hỗ trợ AVX2. Chạy service cùng host với backend; nếu backend ở container thì service cần chung network namespace (`network_mode: service:...` hoặc tiến trình cùng container), vì URL ngoài loopback bị từ chối. Chưa đo tải trên VPS E5 v4, không suy diễn thời gian PC thành SLA VPS.

## Cách xếp hạng và giới hạn

- Tính v2 trên danh sách đã qua kiểm tra quyền, tài khoản/profile active, nhân sự học kỳ và slot trống.
- Encoder tạo vector 384 chiều: tokenizer gốc, tối đa 128 token, attention-mask mean pooling và chuẩn hóa L2. Chia văn bản thành đoạn tối đa 256 ký tự, giữ tối đa 12 đoạn/nguồn. Câu ở cuối CV rất dài có thể nằm ngoài giới hạn này.
- Độ giống ngữ nghĩa là trung bình hai câu dự án khớp nhất với kinh nghiệm; lý do trả đúng một câu kinh nghiệm đã gửi, không dùng AI sinh lời giải thích.
- Với ứng viên có bằng chứng v2: `0.75 × điểm v2 chưa làm tròn + 0.25 × cosine × 100`. Không có kinh nghiệm được đọc thì giữ điểm v2. Đây là **công thức thử nghiệm**, không phải trọng số học mới hay xác suất thành công.
- Thêm ứng viên chưa có bằng chứng từ khóa chỉ khi cosine >= 0.50, hơn ứng viên kế tiếp cùng slot >= 0.08 và không vi phạm kiểm tra phủ định/loại mentor. So margin trên toàn bộ ứng viên kể cả khác batch HTTP. Không lấy học vấn/Bio làm bằng chứng để thêm ứng viên. Ngưỡng/margin được chỉnh bằng bộ phát triển giả lập `semantic-projects.json`; không phải tham số được hiệu chỉnh từ nhãn thực.
- Không tạo nhu cầu IT chỉ từ trùng ngành; hiện vẫn cần chủ đề IT trong danh mục v2 để mở rộng ứng viên IT. Mô tả ngụ ý kỹ thuật hoàn toàn ngoài danh mục có thể bị bỏ sót.
- Câu chứa phủ định trong CV được xử lý bảo thủ; chưa giải quyết mọi ngữ cảnh phủ định tiếng Việt. Đại từ “ai sở hữu” được phân biệt với chữ viết tắt AI.
- Service lỗi/timeout, model sai revision hoặc response thiếu/không hợp lệ trả lỗi ghép mentor an toàn; không âm thầm quay về v2 để báo thành công.

Tham số tùy chọn: `Mentoring__Semantic__BaseUrl`, `PromotionThreshold`, `PromotionMargin`, `Weight` (các mục cuối nằm dưới `Mentoring__Semantic__...`). Đặt `Mentoring__MatchingProvider=Trained` để quay về v2; mặc định ứng dụng vẫn là provider cũ.

## So sánh và đánh giá của con người

```powershell
dotnet run --project tools/mentor-matching/demo/Demo.csproj -- . semantic-projects.json Trained
dotnet run --project tools/mentor-matching/demo/Demo.csproj -- . semantic-projects.json Hybrid
dotnet run --project tools/mentor-matching/demo/Demo.csproj -- . projects.json Hybrid
dotnet run --project tools/mentor-matching/demo/Demo.csproj -- . holdout-projects.json Hybrid
```

File kết quả Hybrid có tiền tố `hybrid-`, không ghi đè kết quả v2. Bộ `semantic-projects.json` là bộ phát triển sau khi tác giả đã xem kết quả; **không gọi là test mù hoặc độ chính xác thực tế**. Cần đánh giá độc lập trên dự án/CV thực đã ẩn dữ liệu cá nhân trước khi bật production.

Tạo phiếu chấm local, không hiện thứ hạng/đáp án kỳ vọng:

```powershell
& $python tools/mentor-matching/semantic/review.py --reports tools/mentor-matching/demo/results.json tools/mentor-matching/demo/holdout-results.json tools/mentor-matching/demo/semantic-results.json --output artifacts/mentor-review/index.html
```

Mở file HTML, đánh giá 0–3 rồi xuất JSONL. Phiếu mẫu có 33 dự án/462 cặp giả lập, không điền sẵn nhãn. Có thể dùng `--data <local-redacted-snapshots.jsonl>` cho dữ liệu thật do người vận hành đã ẩn danh; không tự xuất database/CV thật. Cấu trúc snapshot theo phần `Train bằng nhãn` trong README cấp trên. Mọi cặp để trống phải tiếp tục là chưa biết, không quy thành nhãn 0. Các phiên bản gần giống cùng projectId để tránh rò rỉ train/test. Với dữ liệu thật, nên có người thứ hai kiểm tra các đánh giá bất đồng và các cặp hệ thống từ chối gợi ý.

Train lại head v2 sau khi có ít nhất 5 nhóm dự án có nhãn (mức tối thiểu kỹ thuật, chưa đủ chứng minh chất lượng):

```powershell
& $python tools/mentor-matching/train.py --data tools/mentor-matching/local/mentor-reviewed-labels.jsonl --output artifacts/mentor-matching/reviewed-model.json
```

Lệnh này học lại trọng số **head v2**, không fine-tune encoder và không tự hiệu chỉnh blend/ngưỡng Hybrid. Giữ bộ test độc lập, so Top-1/Top-3, NDCG và số gợi ý sai khi không có mentor phù hợp trước khi thay artifact hoặc ngưỡng. Không dùng kết quả bộ test để tiếp tục chọn trọng số. Không đánh dấu `suitabilityValidated=true` chỉ vì test phần mềm đạt.

## Kiểm thử

```powershell
& $python -m unittest discover -s tools/mentor-matching/semantic -p 'test_*.py' -v
$env:EHUB_TEST_LOCAL_SEMANTIC='1'
dotnet test backend/tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --filter FullyQualifiedName~HybridProviderUsesRealLocalOnnx
```

Test opt-in gọi model local thật qua endpoint có xác thực và upload CV trong Testcontainers; storage CV là fake trong RAM. Các test protocol dùng stub riêng để kiểm tra malformed response, ID, giới hạn body, phân quyền, cancellation và quy tắc chấp nhận ứng viên, không thay thế test model thật.
