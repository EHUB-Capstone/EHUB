# Hướng dẫn kiểm thử thủ công AI phân tích proposal

Tài liệu này mô tả luồng hiện có từ lúc chuẩn bị dữ liệu, sinh viên nộp proposal, background worker phân tích cho đến lúc giảng viên xem Top 3 và ra quyết định. AI chỉ cung cấp thông tin tham khảo; nó không tự động duyệt hoặc từ chối proposal.

## 1. Chọn mức kiểm thử

Nên chạy theo hai vòng:

1. **Smoke test không gọi dịch vụ ngoài**: `Embedding=DeterministicLocal`, `Analysis=Mock`. Vòng này kiểm tra UI, quyền, database, job nền, trạng thái và luồng nộp bài mà không tốn quota.
2. **Kiểm thử AI thật**: `Embedding=Gemini`, `Analysis=Gemini`. Vòng này kiểm tra khả năng tìm proposal tương đồng và sinh nhận xét bằng model thật.

Backend phải bật `Features:AI:Enabled=ON` ở cả hai vòng. Mỗi lần đổi cấu hình cần dừng và khởi động lại backend.

## 2. Chuẩn bị backend local

Từ thư mục gốc repository, bật AI bằng ASP.NET Core User Secrets:

```powershell
dotnet user-secrets set "Features:AI:Enabled" "ON" --project backend/src/EHub.Api
```

### Vòng A — smoke test an toàn

```powershell
dotnet user-secrets set "AI:Embedding:Provider" "DeterministicLocal" --project backend/src/EHub.Api
dotnet user-secrets set "AI:Analysis:Provider" "Mock" --project backend/src/EHub.Api
```

Vòng này không cần API key. Nó xác nhận toàn bộ luồng kỹ thuật nhưng kết quả không đại diện cho chất lượng model AI.

### Vòng B — Gemini thật

Nếu key đã được lưu trong User Secrets thì không cần nhập lại. Cần bảo đảm hai provider đều là `Gemini`, và cả hai nhánh cấu hình đều có key:

```powershell
dotnet user-secrets set "AI:Embedding:Provider" "Gemini" --project backend/src/EHub.Api
dotnet user-secrets set "AI:Embedding:Model" "gemini-embedding-2" --project backend/src/EHub.Api
dotnet user-secrets set "AI:Embedding:Dimension" "768" --project backend/src/EHub.Api
dotnet user-secrets set "AI:Analysis:Provider" "Gemini" --project backend/src/EHub.Api
dotnet user-secrets set "AI:Analysis:Model" "gemini-2.5-flash" --project backend/src/EHub.Api
```

Đặt giá trị key thật cho cả `AI:Embedding:ApiKey` và `AI:Analysis:ApiKey` bằng User Secrets trên máy cá nhân. Không đưa key vào frontend, `appsettings.json`, ảnh chụp màn hình, Git hoặc nội dung gửi cho AI. Một Gemini API key có thể được dùng cho cả hai cấu hình nếu project Google của nhóm cho phép hai API call tương ứng.

Thực hiện trực tiếp trên máy và thay placeholder bằng key của bạn:

```powershell
dotnet user-secrets set "AI:Embedding:ApiKey" "<YOUR_GEMINI_API_KEY>" --project backend/src/EHub.Api
dotnet user-secrets set "AI:Analysis:ApiKey" "<YOUR_GEMINI_API_KEY>" --project backend/src/EHub.Api
```

Khởi động backend; ở môi trường Development, ứng dụng tự chạy migration và seed trước khi nhận request:

```powershell
dotnet run --project backend/src/EHub.Api --launch-profile http
```

Backend dùng `http://localhost:5226`. Giữ terminal này mở để quan sát lỗi nghiệp vụ tổng quát, nhưng không log hoặc chia sẻ credential.

## 3. Khởi động frontend thật

Mở terminal thứ hai:

```powershell
cd frontend
npm install
npm run dev
```

Mở `http://localhost:5173`. Vite chuyển tiếp `/api` sang backend ở cổng `5226`. Bảo đảm `VITE_ENABLE_API_MOCKS=false`; nếu mock frontend đang bật thì bạn không kiểm thử pipeline backend/Gemini thật.

## 4. Chuẩn bị tài khoản và candidate pool

Để Top 3 có ý nghĩa, database cần proposal lịch sử của các team khác. Candidate pool hiện lấy toàn hệ thống, có thể khác lớp và khác học kỳ, nhưng không lấy chính proposal đang phân tích.

Chuẩn bị tối thiểu:

- Một giảng viên được phân công đúng lớp thử nghiệm.
- Một team mục tiêu có Team Leader để nộp proposal cần kiểm tra.
- Ít nhất ba team khác có proposal đã nộp trước đó; tốt nhất tạo 4–6 proposal, gồm hai proposal gần chủ đề và hai proposal khác hẳn.
- Mỗi team phải có Project Workspace, 1–3 Startup Industry và Project Direction ở trạng thái `Approved`.

Nếu database mới hoàn toàn, lặp luồng ở mục 5 cho các team ứng viên trước. Với mỗi team, nhập nội dung khác nhau và nộp proposal. Chỉ dùng dữ liệu giả; không sao chép dữ liệu cá nhân hoặc production để thử nghiệm.

Một bộ dữ liệu kiểm thử dễ quan sát:

- Candidate A: tìm phòng trọ sinh viên đã xác minh.
- Candidate B: ghép bạn ở cùng theo thói quen và ngân sách.
- Candidate C: cứu trợ thực phẩm dư thừa.
- Proposal mục tiêu: gợi ý phòng trọ và bạn ở gần trường.

Kỳ vọng A/B đứng trên C. Với Gemini, thứ tự và diễn giải có thể thay đổi nhẹ nhưng phải có bằng chứng bám vào nội dung proposal.

## 5. Luồng sinh viên nộp proposal

1. Đăng nhập bằng tài khoản **Student** thuộc team mục tiêu. Có thể dùng thành viên thường để kiểm tra quyền chỉnh sửa, nhưng bước nộp bắt buộc dùng **Team Leader**.
2. Mở menu **Startup Workspace** rồi tab **Workspace Overview**.
3. Kiểm tra thẻ **Project Direction** hiển thị `Approved`. Nếu chưa, Team Leader gửi Project Direction và giảng viên được phân công duyệt trước.
4. Bấm **Detailed Project Proposal → Open**.
5. Nhập các trường. Để đủ điều kiện nộp, tối thiểu:
   - Proposal title: 5 ký tự.
   - Startup name: 2 ký tự.
   - Problem statement: 100 ký tự.
   - Proposed solution: 100 ký tự.
   - Target customers: 50 ký tự.
   - Value proposition: 50 ký tự.
   - Business model: 100 ký tự.
   - Roadmap and milestones: 100 ký tự.
   - Tổng nội dung không quá 30.000 ký tự.
6. Bấm **Save draft** hoặc **Save draft version**. Sau khi lưu, trạng thái là `Draft` và lịch sử có một version bất biến.
7. Nếu vừa sửa thêm nội dung, phải lưu lại trước; nút submit không nhận bản chưa lưu.
8. Đăng nhập Team Leader nếu đang dùng thành viên thường, mở lại proposal và bấm **Submit proposal**, sau đó xác nhận hộp thoại.
9. Proposal chuyển sang `Submitted for review` và bị khóa chỉnh sửa. Nếu AI đang ON, backend tạo một analysis job gắn đúng submitted version; request submit không phải chờ Gemini chạy xong.

## 6. Quan sát AI xử lý trên giao diện

Ngay sau khi nộp, trang preview hiển thị **Báo cáo phân tích đề xuất**:

- `Đang chờ phân tích`: job đã tạo, worker chưa nhận.
- `Đang phân tích đề xuất`: worker đã nhận và đang embedding/retrieval/gọi analysis provider.
- `Completed`: giao diện hiện báo cáo. Frontend tự hỏi lại trạng thái mỗi 3 giây; worker kiểm tra job khoảng mỗi 5 giây.
- `Phân tích chưa hoàn tất`: job thất bại; proposal vẫn còn nguyên và giảng viên vẫn có thể review.

Ở vai trò Student, kiểm tra rằng chỉ thấy:

- Mức giao thoa tổng quát.
- Tóm tắt.
- Điểm khác biệt tiềm năng.
- Giới hạn cần lưu ý.
- Thông tin phiên bản provider/model ở cuối báo cáo.

Student **không được thấy** Top proposal tương đồng, snapshot proposal khác, điểm chi tiết hoặc bằng chứng trích từ proposal khác. Đây là kiểm tra phân quyền quan trọng, không chỉ là ẩn bằng CSS.

## 7. Luồng giảng viên xem báo cáo chi tiết

1. Đăng xuất Student và đăng nhập **Lecturer được phân công** cho lớp của team mục tiêu.
2. Mở menu **Startup Workspace**.
3. Chọn đúng lớp/team rồi bấm **Open team workspace**.
4. Tại **Workspace Overview**, mở **Detailed Project Proposal**. Giảng viên được đưa vào preview read-only.
5. Chờ trạng thái hoàn tất nếu job vẫn đang chạy.
6. Kiểm tra phần báo cáo chi tiết:
   - Mức giao thoa `Chưa đủ dữ liệu/Thấp/Trung bình/Cao`.
   - Top 3 proposal tương đồng và số ứng viên đã được xếp hạng lại.
   - Điểm Hybrid, semantic có trọng số, TF-IDF, Jaccard và embedding tổng thể.
   - Điểm theo bốn trường: vấn đề, giải pháp, khách hàng, giá trị/cách làm.
   - Điểm tương đồng, điểm khác biệt, yếu tố mới tiềm năng.
   - Bằng chứng được trích từ proposal hiện tại hoặc proposal đối sánh.
   - Khối so sánh song song; đổi proposal trong bộ chọn để xem từng candidate.
7. Đối chiếu bằng mắt: trích dẫn phải thật sự tồn tại trong snapshot hiển thị; nhận xét không được khẳng định đạo văn hoặc tự kết luận duyệt/từ chối.
8. Ở khối **Lecturer decision**, chọn `Approve`, `Request revision` hoặc `Reject`, nhập feedback 3–1000 ký tự, bấm **Record decision** và xác nhận.

AI không điều khiển bước 8. Quyết định cuối cùng luôn do giảng viên thực hiện và được lưu riêng trong review history.

## 8. Kiểm tra resubmit và tính bất biến

1. Giảng viên chọn **Request revision** và ghi yêu cầu cụ thể.
2. Đăng nhập lại Team Leader hoặc thành viên trong team. Proposal ở trạng thái `Needs revision`, hiển thị feedback và được phép sửa.
3. Sửa nội dung, lưu thành draft version mới.
4. Team Leader submit lại.
5. Xác nhận hệ thống tạo analysis job/report mới gắn với version vừa nộp; báo cáo cũ không bị ghi đè. Trong **Version history**, các submitted snapshot cũ vẫn phải xem được và không thay đổi.

## 9. Kiểm tra chế độ OFF

Sau khi hoàn tất thử nghiệm, tắt AI và khởi động lại backend:

```powershell
dotnet user-secrets set "Features:AI:Enabled" "OFF" --project backend/src/EHub.Api
```

Tạo hoặc resubmit một proposal mới rồi kiểm tra:

- Nộp proposal và review của giảng viên vẫn hoạt động bình thường.
- Không tạo analysis job mới.
- API proposal không trả `currentAnalysisJobId`, vì vậy giao diện không hiện panel AI.
- Bật lại AI sau đó không tự phân tích bù proposal đã nộp khi OFF; cần một lần resubmit/version mới nếu muốn tạo job.

Không cần xóa key khi chuyển OFF. Tuy nhiên, production/VPS vẫn phải giữ key trong environment variable hoặc secret storage, không lưu trong Git.

## 10. Checklist đạt yêu cầu

- [ ] OFF: nộp/review bình thường, không có job hoặc panel AI.
- [ ] ON + local/mock: job đi `Pending → Processing → Completed` mà không gọi dịch vụ ngoài.
- [ ] ON + Gemini: Top 3 hợp lý với candidate pool và báo cáo sinh thành công.
- [ ] Student không thấy dữ liệu chi tiết của proposal khác.
- [ ] Lecturer được phân công thấy chi tiết; người không có quyền nhận 403/không truy cập được team.
- [ ] Báo cáo có disclaimer và không tự đưa ra quyết định duyệt.
- [ ] Resubmit tạo report mới, không ghi đè report/version cũ.
- [ ] Lỗi provider không làm mất proposal và không chặn Lecturer review.
- [ ] Không có API key, raw exception hoặc dữ liệu nhạy cảm trong browser console, log và ảnh chụp.

## 11. Xử lý lỗi thường gặp

- **Không thấy panel AI:** AI đang OFF, proposal được submit khi OFF, frontend mock đang bật, hoặc proposal chưa có `currentAnalysisJobId`. Bật ON, restart backend và submit một version mới.
- **Mãi ở Pending:** backend worker không chạy, database chưa cập nhật migration, hoặc instance xử lý job đã dừng. Kiểm tra terminal backend và kết nối database.
- **Failed ngay khi dùng Gemini:** kiểm tra provider name, model, dimension và việc key được lưu ở cả `AI:Embedding` lẫn `AI:Analysis`. Không chụp/đăng giá trị key.
- **Không có Top 3:** candidate pool chưa có proposal lịch sử đủ điều kiện. Tạo ít nhất ba proposal của team khác và submit trước proposal mục tiêu.
- **Student thấy Top 3:** coi đây là lỗi phân quyền nghiêm trọng; dừng demo và kiểm tra API response trước khi tiếp tục.
- **429/quota:** chờ quota phục hồi hoặc giảm số lần thử; không submit lặp liên tục. Proposal không bị mất và job có cơ chế retry đối với lỗi tạm thời.
