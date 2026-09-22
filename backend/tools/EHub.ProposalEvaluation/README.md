# Đánh giá xếp hạng proposal bằng Golden Dataset

Công cụ này chạy độc lập với API và database để đo chất lượng thứ tự proposal tương đồng. Nó dùng đúng các thành phần chấm điểm của production và so sánh bốn phương pháp:

- Embedding-only: cosine theo bốn trường ngữ nghĩa có trọng số.
- TF-IDF-only: độ giống từ khóa có điều chỉnh độ hiếm.
- Hybrid/full corpus: semantic + TF-IDF + Jaccard trên toàn bộ ứng viên.
- Production two-stage hybrid: lấy Top 10 bằng embedding tổng thể rồi xếp hạng lại bằng hybrid, giống luồng production.

Các chỉ số được báo cáo tại `K` là `Precision@K`, `Recall@K` và `NDCG@K`. Báo cáo JSON còn lưu hash SHA-256 của dataset, phiên bản thuật toán, trọng số, provider/model embedding và kết quả từng test case để lần chạy sau có thể đối chiếu.

## Dataset ban đầu

`Data/proposal-golden-dataset-v1.json` có 30 proposal tổng hợp, gồm nội dung tiếng Anh và tiếng Việt, cùng 10 test case. Dữ liệu không lấy từ người dùng hay production.

Trạng thái hiện tại là `seed-unreviewed`: nhãn liên quan chỉ là nhãn khởi tạo kỹ thuật, chưa phải kết luận khoa học. Trước khi đưa số liệu vào báo cáo đồ án, tối thiểu hai thành viên hoặc giảng viên có chuyên môn phải đánh giá độc lập từng cặp theo thang:

- `3`: rất liên quan, gần như cùng bài toán và hướng giải pháp.
- `2`: liên quan rõ ràng nhưng khác một phần quan trọng.
- `1`: liên quan yếu nhưng vẫn hữu ích khi đối chiếu.
- Không ghi ID: không liên quan.

Sau khi xử lý bất đồng và chốt nhãn, đổi `labelStatus` sang một tên phiên bản rõ ràng, ví dụ `reviewed-v1-two-reviewers`. Không thay nội dung dataset sau khi công bố kết quả mà không tăng phiên bản và chạy lại benchmark.

## Chạy offline, không tốn API

Từ thư mục `backend/`:

```powershell
dotnet run --project tools/EHub.ProposalEvaluation/EHub.ProposalEvaluation.csproj -- --provider local --k 3
```

Có thể xuất báo cáo để lưu cùng hồ sơ thí nghiệm:

```powershell
dotnet run --project tools/EHub.ProposalEvaluation/EHub.ProposalEvaluation.csproj -- --provider local --k 3 --json artifacts/proposal-evaluation-local.json
```

Provider `local` dùng feature hashing có tính xác định, phù hợp kiểm tra pipeline, metric và khả năng tái lập. Kết quả này không chứng minh chất lượng hiểu ngữ nghĩa của mô hình AI.

## Chạy với Gemini embedding

Chỉ đặt key trong biến môi trường của phiên terminal hiện tại; không ghi key vào command history, source code, dataset hoặc báo cáo. Công cụ đọc:

- `AI__Embedding__ApiKey` (bắt buộc)
- `AI__Embedding__Model` (mặc định `gemini-embedding-2`)
- `AI__Embedding__Dimension` (mặc định `768`)

Sau khi các biến môi trường đã được đặt an toàn:

```powershell
dotnet run --project tools/EHub.ProposalEvaluation/EHub.ProposalEvaluation.csproj -- --provider gemini --k 3 --json artifacts/proposal-evaluation-gemini.json
```

Gemini mode gửi nội dung synthetic của dataset ra provider và có thể phát sinh quota/chi phí. Không thay dataset bằng dữ liệu production hoặc dữ liệu cá nhân chưa được phép xử lý.

## Cách đọc kết quả

- `Precision@3`: trong ba kết quả đầu, tỷ lệ bao nhiêu là proposal đã được gắn nhãn liên quan.
- `Recall@3`: ba kết quả đầu tìm lại được bao nhiêu phần trong toàn bộ proposal liên quan đã biết.
- `NDCG@3`: không chỉ xét tìm đúng mà còn thưởng khi proposal có mức liên quan cao đứng trước; `1.0` là thứ tự lý tưởng.

Chỉ so sánh hai lần chạy khi dataset hash, `K`, provider/model và dimension tương thích. Không điều chỉnh trọng số trực tiếp trên cùng bộ test rồi công bố kết quả đó như đánh giá độc lập; nếu cần tinh chỉnh, hãy tách tập tuning và tập test cuối.
