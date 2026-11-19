# Kịch bản báo cáo — Module Project Showcase

Thời lượng gợi ý: 12–15 phút (demo trực tiếp). Mỗi phần ghi: **Nói** (lời thuyết trình), **Làm** (thao tác trên màn hình), **Kết quả** (điều khán giả cần thấy).

> Các điểm đánh dấu ⚠ là chỗ mình dựa vào code nhưng chưa chạy thử trên giao diện — hãy tự bấm thử một lần trước giờ báo cáo.

---

## 0. Chuẩn bị trước khi báo cáo (10 phút)

Cần 3 tài khoản và dữ liệu sẵn:

| Tài khoản | Vai trò | Dùng để |
|---|---|---|
| Sinh viên A (thành viên một team) | STUDENT | tạo và gửi duyệt trang showcase |
| Sinh viên B (team khác) | STUDENT | upvote, bình luận, báo cáo |
| Giảng viên (hoặc Admin) | LECTURER / ADMIN | duyệt, ghim Featured, xử lý báo cáo |

Dữ liệu nên có sẵn:
- Một dự án đã điền đủ **Description, Problem, Solution** trong Project Profile (bắt buộc để gửi duyệt) và một dự án chưa điền để demo lỗi.
- Vài dự án đã Published ở nhiều kỳ học và nhiều thời điểm (tuần này, tuần trước, cũ hơn) để thấy các mục của Gallery.
- 2–3 ảnh PNG/JPG nhỏ hơn 5 MB và một link YouTube.
- Bật cờ `releaseFeatureFlags.showcase`, nếu không các trang `/showcase` sẽ không hiện.

---

## 1. Mở đầu (1 phút)

**Nói:** Showcase là nơi các team sinh viên trưng bày sản phẩm startup ra cộng đồng E-HUB. Module có ba nhóm việc: (1) sinh viên tạo và gửi trang giới thiệu, (2) giảng viên duyệt và quản lý nội dung, (3) mọi người khám phá, vote, bình luận. Dữ liệu tên, mô tả, vấn đề, giải pháp lấy từ Project Profile nên hai trang không bao giờ mâu thuẫn.

**Làm:** Hiển thị sơ đồ trạng thái:

```
Draft ──Submit──▶ PendingReview ──Approve──▶ Published ──Hide──▶ Hidden
  ▲                    │                          ▲                │
  └──── Reject ────────┘                          └──── Unhide ────┘
  ▲                    │
  └──── Withdraw ──────┘
```

**Kết quả:** Khán giả hiểu 4 trạng thái: Draft, PendingReview, Published, Hidden.

---

## 2. Phân quyền (30 giây)

| Vai trò | Được làm gì |
|---|---|
| Mọi người dùng đã đăng nhập | Xem Gallery, xem chi tiết, upvote, bình luận, báo cáo |
| Sinh viên (STUDENT) | Tạo, sửa, gửi duyệt, rút lại trang showcase của dự án mình; xem Insights của trang mình |
| Admin | Duyệt, từ chối, ẩn, khôi phục, ghim Featured, xử lý báo cáo, xóa bình luận cho **mọi** trang |
| Giảng viên (Lecturer) | Các thao tác trên, nhưng chỉ với trang của dự án thuộc lớp mình là giảng viên chính hoặc được phân công |

**Nói:** Quyền được kiểm tra ở backend (policy `StudentOnly`, `StaffOnly`), không chỉ ẩn nút ở giao diện. Sinh viên chỉ sửa được trang thuộc dự án của team mình.

---

## 3. Sinh viên tạo và gửi trang showcase (3 phút)

**Đăng nhập:** Sinh viên A → vào `/showcase/manage` (My Showcase).

### 3.1 Tạo trang
**Làm:** Chọn dự án → tạo trang → vào trình soạn thảo `/showcase/manage/:lineageId`.
**Kết quả:** Trang ở trạng thái **Draft**. Mỗi dự án chỉ có một trang (tạo lần hai sẽ báo "This project already has a showcase page").

### 3.2 Soạn nội dung
**Làm:** Lần lượt thao tác:
- Nhập **Highlights** (tối đa 2.000 ký tự).
- Chọn **giai đoạn phát triển**: Idea, MVP, Beta, Launched.
- Thêm **liên kết** (tối đa 8, mỗi liên kết có nhãn tối đa 100 ký tự). Link video chỉ nhận YouTube hoặc Vimeo; hệ thống dựng lại link chuẩn từ video id nên không thể chèn link giả.
- Tải **logo** và **ảnh** (PNG, JPG, WEBP; mỗi ảnh tối đa 5 MB; tối đa 8 ảnh). Thử tải một file không phải ảnh để cho thấy bị từ chối vì kiểm tra cả nội dung file chứ không chỉ đuôi.
- Bật/tắt **hiển thị thành viên team**.

**Kết quả:** Lưu thành công; nếu hai người cùng sửa, người lưu sau nhận thông báo "changed by someone else" (khóa xung đột `rowVersion`).

### 3.3 Gửi duyệt và rút lại
**Làm:**
1. Với dự án **chưa** điền Problem/Solution → bấm Submit → báo lỗi "Complete the Description, Problem and Solution in your Project Profile before submitting."
2. Với dự án đủ thông tin → tích ô xác nhận nội dung được phép công khai → Submit.
3. Trạng thái chuyển **PendingReview**. Thử sửa → bị chặn ("cannot be edited while waiting for review").
4. Bấm Withdraw → về **Draft** (để demo tiếp, gửi duyệt lại).

**Kết quả:** Điều kiện gửi duyệt (hồ sơ đầy đủ + xác nhận công khai) và khóa sửa khi đang chờ duyệt.

---

## 4. Giảng viên duyệt nội dung (2 phút)

**Đăng nhập:** Giảng viên → `/showcase/moderation`.

**Làm:**
1. Mở hàng đợi, lọc theo trạng thái; thấy trang vừa gửi (hiện tên dự án, team, mã lớp, giai đoạn).
2. **Reject** thử một trang: hệ thống bắt buộc nhập ghi chú (tối đa 1.000 ký tự) → trang về Draft, sinh viên nhận thông báo kèm lý do.
3. **Approve** trang còn lại → chuyển **Published**, sinh viên nhận thông báo.
4. **Hide** một trang đã Published (bắt buộc ghi chú) → trang biến mất khỏi Gallery. **Unhide** để khôi phục.

| Hành động | Từ → Đến | Ghi chú bắt buộc |
|---|---|---|
| Approve | PendingReview → Published | Không |
| Reject | PendingReview → Draft | Có |
| Hide | Published → Hidden | Có |
| Unhide | Hidden → Published | Không |

**Nói:** Mỗi quyết định được lưu lịch sử duyệt và gửi thông báo qua cơ chế Outbox, cùng một giao dịch với thay đổi dữ liệu nên không bị mất thông báo khi lỗi giữa chừng.

---

## 5. Gallery — khám phá dự án (3 phút) ⭐ phần quan trọng nhất

**Đăng nhập:** bất kỳ người dùng nào → `/showcase`.

### 5.1 Các mục hiển thị

| Mục | Cách tính | Hiển thị |
|---|---|---|
| **Featured** | Giảng viên ghim tay, không giới hạn thời gian | 30 dự án, mới nhất trước |
| **Trending now** | Hoạt động 7 ngày gần nhất: mỗi upvote 3 điểm, mỗi bình luận 2 điểm, mỗi người xem trong ngày 1 điểm. Không phụ thuộc ngày publish; dự án không có hoạt động thì không vào | 5 dự án, không có "See all" |
| **This week's** | Publish trong tuần lịch hiện tại (thứ Hai–Chủ nhật, theo UTC) | 12 dự án, "See all" mở tối đa 50 |
| **Last week's** | Publish trong tuần lịch trước | 12 dự án, "See all" mở tối đa 50 |
| **Earlier projects** | Publish trước thứ Hai của tuần trước | 12 dự án mỗi trang, có phân trang |

Quy tắc hiển thị:
- Mục nào rỗng thì **ẩn**. Tuần/tháng không có dự án mới thì chỉ còn Featured và Earlier, dự án cũ không bị mất.
- Dự án đã ở Trending không lặp lại ở This week và Last week.
- Featured được phép lặp với mục khác vì là mục ghim tay; dự án Featured có **dấu sao** cạnh tên.
- Sắp xếp theo thời gian publish, không theo kỳ học.

### 5.2 Tìm kiếm và bộ lọc
**Làm:** Gõ từ khóa, chọn Stage, Industry. Bộ lọc áp dụng cho mọi mục. Chọn dropdown **Discover** để xem riêng một mục.

### 5.3 Bộ lọc Semester
**Làm:** Mở dropdown Semester.
**Kết quả:** Chỉ hiện kỳ có dự án đã publish, kèm số lượng (ví dụ "FA26 (3)"). Chọn một kỳ → trang chuyển sang **chế độ xem theo kỳ**: ẩn Trending, This week, Last week, Earlier; chỉ còn Featured của kỳ đó và mục "Projects in FA26" (tất cả dự án của kỳ, mới nhất trước, phân trang 12 dự án).

**Nói:** Chế độ theo kỳ tồn tại vì các mục theo tuần không có nghĩa khi xem lại một kỳ cũ.

---

## 6. Trang chi tiết dự án và tương tác (2 phút)

**Làm:** Sinh viên B mở một dự án `/showcase/:id`.

| Chức năng | Thao tác | Quy tắc |
|---|---|---|
| **Upvote** | Bấm nút lên; bấm lại để bỏ vote | Không được tự upvote dự án của team mình |
| **Lượt xem** | Mở trang | Mỗi người được tính một lượt mỗi ngày (khóa dữ liệu theo dự án, người dùng, ngày) ⚠ |
| **Bình luận** | Viết bình luận, trả lời một bình luận | Tối đa 2.000 ký tự; trả lời của thành viên team được đánh dấu "team reply"; thông báo gửi cho thành viên team hoặc tác giả bình luận gốc |
| **Xóa bình luận** | Xóa bình luận của mình | Tác giả được xóa bình luận của mình; Admin và giảng viên phụ trách lớp của dự án được xóa để kiểm duyệt; người khác nhận "You cannot remove this comment" |
| **Báo cáo** | Bấm Report trên trang hoặc một bình luận, chọn lý do | Lý do: Spam, Harassment, InappropriateContent, CopyrightViolation, Other; không báo cáo nội dung của chính mình; mỗi người báo cáo một nội dung một lần |

**Insights (chỉ sinh viên chủ dự án):** Đăng nhập lại sinh viên A → trang chi tiết dự án của mình → xem biểu đồ lượt xem, upvote, bình luận theo ngày, so sánh 14 ngày gần nhất với 14 ngày trước đó. Người khác gọi API này sẽ nhận 404.

---

## 7. Giảng viên ghim Featured (1 phút)

**Đăng nhập:** Giảng viên → `/showcase` → bấm **Select featured**.

**Làm:** Lọc theo lớp hoặc giảng viên, chọn nhiều dự án rồi ghim cùng lúc; bỏ ghim tương tự. Thử bật/tắt Featured riêng một dự án ở trang chi tiết.

**Kết quả:** Dự án xuất hiện trong mục Featured với dấu sao. Thao tác hàng loạt tối đa 500 dự án mỗi lần; dự án đã ở đúng trạng thái được báo là "unchanged", dự án không tồn tại được báo là "skipped" thay vì lỗi cả loạt.

---

## 8. Xử lý báo cáo vi phạm (1 phút)

**Đăng nhập:** Giảng viên → khu vực báo cáo (trong trang `/showcase/moderation`) ⚠.

**Làm:**
1. Xem danh sách báo cáo đang **Open** (giảng viên của lớp có dự án bị báo cáo được thông báo khi có báo cáo mới).
2. Xử lý một báo cáo: chọn **Dismissed** (bác bỏ) hoặc **ActionTaken** (đã xử lý), kèm ghi chú tối đa 1.000 ký tự. Nếu báo cáo nhắm vào bình luận và đã xử lý, tác giả bình luận nhận thông báo.
3. Xóa lịch sử báo cáo đã xử lý. Báo cáo **Open** không xóa được.

**Nói:** Hai giảng viên cùng mở một báo cáo thì người xử lý sau nhận thông báo "changed by another reviewer" nhờ kiểm soát xung đột.

---

## 9. Chất lượng và bảo mật (1 phút)

- Kiểm thử đã chạy: 157 test backend lớp Application, 92 integration test cho Showcase (chạy trên PostgreSQL thật qua Docker), 43 test frontend liên quan (Showcase, dark mode, design token).
- Bảo mật: mọi API cần đăng nhập; thao tác duyệt, ghim, xử lý báo cáo giới hạn cho Staff; thao tác quản lý trang giới hạn cho sinh viên sở hữu dự án; file ảnh kiểm tra loại, kích thước và chữ ký nội dung; link video chuẩn hóa.
- Tin cậy: thông báo qua Outbox cùng giao dịch dữ liệu; khóa xung đột khi nhiều người cùng sửa.

---

## 10. Kết luận và hướng phát triển (30 giây)

Đã hoàn thành: vòng đời trang showcase, duyệt nội dung, khám phá theo tuần và theo kỳ, Trending, tương tác cộng đồng, báo cáo vi phạm, thống kê cho chủ dự án.

Hướng phát triển (nói nếu được hỏi):
- Thêm integration test riêng cho truy vấn Trending trên PostgreSQL và đo hiệu năng với dữ liệu lớn (truy vấn đếm upvote, bình luận, lượt xem cho từng dự án).
- Chặn gian lận vote (ví dụ giới hạn tài khoản mới).
- Mục "Most upvoted" hoặc Top theo ngành nếu cộng đồng đủ lớn.

---

## Phụ lục A — Câu hỏi dự kiến và gợi ý trả lời

| Câu hỏi | Trả lời ngắn |
|---|---|
| Tuần nào không có dự án mới thì trang hiển thị gì? | Các mục tuần rỗng bị ẩn; còn Featured, Trending (nếu có hoạt động) và Earlier; dự án cũ không biến mất |
| Hiển thị tối đa bao nhiêu dự án? | Mỗi mục có số mặc định (Featured 30, Trending 5, tuần 12). "See all" mở tối đa 50. Earlier và chế độ theo kỳ phân trang không giới hạn tổng |
| Vì sao Trending và Featured có thể trùng? | Featured là ghim tay, Trending là xếp theo hoạt động; hai ý nghĩa khác nhau nên cho phép trùng, có dấu sao để người xem hiểu |
| Vì sao Trending không lấy theo ngày publish? | Để tuần vắng dự án mới vẫn có nội dung, và dự án cũ được quan tâm lại có thể nổi lên |
| Chọn kỳ học thì thấy gì? | Chế độ xem theo kỳ: Featured của kỳ và toàn bộ dự án của kỳ, phân trang |
| Vote có bị gian lận không? | Chặn tự vote dự án của mình; chưa có chống gian lận nâng cao (hướng phát triển) |
| Ai thấy được Insights? | Chỉ sinh viên thuộc team sở hữu dự án |
| Giảng viên nào duyệt được dự án nào? | Admin duyệt mọi dự án; giảng viên chỉ duyệt dự án thuộc lớp mình là giảng viên chính hoặc được phân công (tính theo phiên bản dự án mới nhất của team) |

## Phụ lục B — Danh sách API (tra cứu nhanh)

| Nhóm | Endpoint |
|---|---|
| Khám phá | `GET /api/showcase`, `GET /api/showcase/filter-options`, `GET /api/showcase/{id}` |
| Quản lý (STUDENT) | `GET /mine`, `POST /`, `PUT /{id}`, `POST /{id}/logo`, `POST /{id}/media`, `DELETE /{id}/media/{mediaId}`, `POST /{id}/submit`, `POST /{id}/withdraw` |
| Duyệt (Staff) | `GET /moderation`, `POST /{id}/approve`, `/reject`, `/hide`, `/unhide` |
| Featured (Staff) | `POST/DELETE /{id}/feature`, `POST /feature/bulk`, `GET /feature/candidates` |
| Tương tác | `POST/DELETE /{id}/upvote`, `POST /{id}/view`, `GET/POST /{id}/comments`, `GET /{id}/comments/{commentId}/replies`, `DELETE /{id}/comments/{commentId}`, `GET /{id}/insights` |
| Báo cáo | `POST /{id}/reports`, `GET /reports`, `POST /reports/{reportId}/resolve`, `DELETE /reports/{reportId}`, `DELETE /reports/history` |

Tất cả nằm dưới `/api/showcase`.
