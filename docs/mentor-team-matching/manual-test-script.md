# Hướng dẫn kiểm thử thủ công theo từng yêu cầu: gán mentor cho nhóm theo kỳ

Tài liệu này giúp bạn kiểm tra trên giao diện **từng yêu cầu của cô** và cho ra một kết luận rõ ràng: **Đạt** hay **Chưa đạt**. Mỗi yêu cầu có một mục riêng, tự đủ ý. Bạn không cần biết code.

> **Chỉ làm trên môi trường thử (dev).** Một số bước ghi dữ liệu thật. Không làm trên dữ liệu thật của nhà trường.

---

## Phần 1. Đọc trước khi bắt đầu

### 1.1 Tính năng này làm gì

Khi sang kỳ mới, Admin cần gán mentor cho các nhóm. Tính năng giúp:

1. **Giữ mentor cũ** cho những nhóm tiếp tục từ kỳ trước (nhóm EXE101 của kỳ trước học tiếp EXE201 ở kỳ này).
2. **Chia mentor** cho những chỗ còn trống (chia cân bằng hoặc ngẫu nhiên).
3. Cho Admin **xem trước**, **sửa tay**, rồi mới **xác nhận lưu**. Chưa xác nhận thì không có gì bị đổi.
4. **Xuất file Excel** danh sách phân công của cả kỳ.

### 1.2 Một vài từ dùng trong tài liệu

| Từ | Nghĩa |
|---|---|
| Mentor doanh nghiệp | Mentor đến từ công ty. Trên màn hình gọi là **Industry mentor**. |
| Mentor giảng viên | Mentor là giảng viên của trường. Trên màn hình gọi là **Lecturer mentor**. |
| Vị trí mentor | Mỗi nhóm có đúng **hai vị trí**: một cho mentor doanh nghiệp, một cho mentor giảng viên. |
| Nhóm tiếp tục | Nhóm EXE201 của kỳ mới đi tiếp nguyên nhóm từ một nhóm EXE101 của kỳ cũ. |
| Xem trước | Bản nháp kết quả, chỉ để xem và sửa, **chưa lưu**. Nút để mở là **Preview assignment**. |
| Tham gia (active) | Mentor được chọn tham gia một kỳ. Một mentor có thể tham gia kỳ này nhưng không tham gia kỳ khác. |

### 1.3 Các nhãn tiếng Anh trên màn hình

| Tên trên màn hình | Ý nghĩa |
|---|---|
| Tab **By team** | Xem theo từng nhóm: nhóm nào có mentor nào. |
| Tab **By mentor** | Xem theo từng mentor: mỗi người hướng dẫn bao nhiêu nhóm. |
| Tab **Needs attention** | Những chỗ cần chú ý: xung đột, mentor không giữ được, vị trí còn thiếu. |
| Nhãn **Current** (xám) | Mentor **đã có sẵn** ở nhóm từ trước. Xem trước không đụng tới. |
| Nhãn **Kept** (xanh dương) | Mentor cũ được **giữ lại** cho nhóm tiếp tục. |
| Nhãn **New** (xanh lá) | Mentor **mới do hệ thống chọn** cho chỗ trống. |
| Nhãn **Manual** (vàng) | Mentor do **bạn chọn tay**. |
| Nhãn **Missing** (đỏ) | Vị trí **còn trống**, chưa có mentor. |
| Nút **Change** / **Undo** | Sửa tay mentor của một vị trí / hoàn tác chỉnh sửa đó. |
| Nút **Regenerate** | Tạo lại bản xem trước từ đầu (các chỉnh sửa tay sẽ mất). |
| Nút **Confirm and save** | **Lưu thật.** Chỉ bấm khi đã kiểm tra xong. |

### 1.4 Cách kết luận từng yêu cầu

Mỗi yêu cầu gồm vài "Kiểm tra" nhỏ. Với mỗi kiểm tra, tick ☐ nếu các dòng "Phải thấy gì" đều đúng. Cuối mỗi mục có ô **Kết luận**:

- **Đạt:** mọi kiểm tra trong mục đều đạt.
- **Đạt có điều kiện:** đạt phần cốt lõi nhưng có điểm nhỏ cần sửa (ghi rõ điểm nào).
- **Chưa đạt:** có ít nhất một kiểm tra cốt lõi sai. Ghi **SAI**, chụp ảnh màn hình, viết ngắn "đã làm gì, thấy gì, đáng lẽ thấy gì".

### 1.5 Thứ tự làm và khi nào dữ liệu bị thay đổi

Các yêu cầu dùng chung một bộ dữ liệu, và "lưu" làm đổi dữ liệu đó. Vì vậy **làm đúng thứ tự dưới đây** (không theo thứ tự số):

| Lượt | Yêu cầu | Ghi dữ liệu? |
|---|---|---|
| 1 | REQ-01 Mentor tham gia theo từng kỳ | Không |
| 2 | REQ-02 Giữ mentor cho nhóm tiếp tục | Không |
| 3 | REQ-03 Xử lý mentor của nhóm đã hoàn thành | Không |
| 4 | REQ-05 Hai cách chia: Random và Balanced | Không |
| 5 | REQ-06 Quy tắc số lượng mentor | Không (có đổi tạm rồi trả lại) |
| 6 | REQ-04 Chia vào vị trí thiếu và sửa tay | Chỉ phần chuẩn bị (gán tay vài mentor) |
| 7 | REQ-07 Xem trước và xác nhận | **Có (LƯU LẦN 1)** |
| 8 | REQ-08 Xung đột và thay thế mentor | **Có (LƯU LẦN 2)** |
| 9 | REQ-09 Xuất file Excel | Không |
| 10 | Kiểm tra chung (phân quyền, giao diện, lỗi mạng, chức năng cũ) | Không |

Các lượt 1 đến 5 **không lưu gì**, làm lại bao nhiêu lần cũng được. Từ lượt 6 dữ liệu bắt đầu thay đổi. Nếu lỡ lưu sớm, hãy dựng lại dữ liệu mẫu (Phần 2) rồi làm lại.

---

## Phần 2. Chuẩn bị dữ liệu mẫu

Bạn cần dựng một bộ dữ liệu nhỏ để các kiểm tra có kết quả đoán trước được. Làm đúng theo bảng, **đừng đổi tên**.

> Một số tên nút có thể hơi khác mô tả bên dưới. Nếu vậy cứ dùng nút tương đương và ghi chú lại để cập nhật tài liệu.

### 2.1 Tài khoản và trình duyệt

- Đăng nhập bằng tài khoản **Admin** (ví dụ `http://localhost:5173`).
- Chuẩn bị một tài khoản **giảng viên** (làm giảng viên chính của một lớp thử) để thử phân quyền.
- Mở được **hai tab trình duyệt** cùng lúc (dùng ở Kiểm tra 7.3).

### 2.2 Chọn hai học kỳ (không cần bật kỳ mới)

Bạn **không cần** bấm **Activate** cho kỳ mới. Hệ thống cho phép tạo lớp ở **kỳ đang Active** và ở **kỳ kế tiếp đã lập kế hoạch** (nhãn **Upcoming**), và các chức năng chia mentor chạy được trên kỳ đã lập kế hoạch. Vì vậy:

- **Kỳ cũ** = kỳ đang Active (ví dụ **FA 2026**).
- **Kỳ mới** = kỳ kế tiếp đang Upcoming (ví dụ **SP 2027**). Để nguyên, không cần Activate.

Lưu ý:
- Nút **Activate** chỉ bấm được khi **ngày hôm nay** nằm trong khoảng ngày của kỳ, nên với kỳ Upcoming nó luôn bị khóa. Bình thường.
- Khi sửa ngày kỳ, **Semester và Year bị khóa** và ngày phải nằm trong năm của kỳ. Đừng cố sửa ngày để bật kỳ.
- Bước **Complete Class** ở kỳ cũ (mục 2.4) là tùy chọn. Nếu bỏ qua, các phân công cũ vẫn Active và vẫn được giữ cho nhóm tiếp tục.

### 2.3 Tạo mentor

Vào `/admin/subjects`, mở tab **Lecturers & Mentors by Semester**, chọn kỳ, rồi dùng **Import mentor list** (bấm **Download template** để lấy file mẫu) hoặc **Add mentor**. Tạo các mentor sau:

| Tên mentor | Loại | Kỳ cũ | Kỳ mới |
|---|---|---|---|
| Mentor Doanh nghiệp **An** | doanh nghiệp | tham gia | tham gia |
| Mentor Doanh nghiệp **Bình** | doanh nghiệp | tham gia | tham gia |
| Mentor Doanh nghiệp **Cường** | doanh nghiệp | tham gia | **KHÔNG tham gia** |
| Mentor Doanh nghiệp **Dũng** | doanh nghiệp | tham gia | tham gia |
| Mentor Doanh nghiệp **Em** | doanh nghiệp | không có | tham gia (mới) |
| Mentor Doanh nghiệp **Phúc** | doanh nghiệp | không có | tham gia (mới) |
| Mentor Giảng viên **Hoa** | giảng viên | tham gia | tham gia |
| Mentor Giảng viên **Lan** | giảng viên | tham gia | **KHÔNG tham gia** |
| Mentor Giảng viên **Mai** | giảng viên | tham gia | tham gia |
| Mentor Giảng viên **Nga** | giảng viên | không có | tham gia (mới) |

Mẹo: điền cột "Loại HĐ" của mentor doanh nghiệp là "Thỉnh giảng" hoặc "Khoán" (xen kẽ) để kiểm tra file Excel sau này.

### 2.4 Tạo lớp và nhóm ở **kỳ cũ**

1. Tạo **một lớp EXE101** với **4 nhóm**: **Nhóm Sao**, **Nhóm Trăng**, **Nhóm Mây**, **Nhóm Gió**. Mỗi nhóm có ít nhất 4 sinh viên (đủ điều kiện kế thừa), có tên dự án và link Zalo.
2. Tạo **một lớp EXE201** với **2 nhóm**: **Nhóm Núi**, **Nhóm Sông**.
3. Gán mentor: vào trang chi tiết lớp (`/classes/...`), ở khối Mentors bấm **Manage**, tab **Assign new**. Chọn **một mentor**, tick **nhiều nhóm** cùng lúc rồi bấm **Assign to N teams**. Hộp thoại vẫn mở để bạn gán tiếp mentor khác. Gán theo bảng:

| Nhóm (kỳ cũ) | Mentor doanh nghiệp | Mentor giảng viên |
|---|---|---|
| Nhóm Sao | An | Hoa |
| Nhóm Trăng | Bình | Lan |
| Nhóm Mây | Cường | Hoa |
| Nhóm Gió | Dũng | Hoa |
| Nhóm Núi (EXE201) | An | Hoa |
| Nhóm Sông (EXE201) | Bình | Mai |

4. (Tùy chọn) Bấm **Complete Class** cho **cả hai lớp** của kỳ cũ.

Bảng này được thiết kế để mỗi quy tắc đều có dữ liệu chứng minh: Hoa vừa có nhóm tiếp tục (Sao, Mây), vừa có nhóm không tiếp tục (Gió), vừa có nhóm đã học xong (Núi); Lan và Cường sẽ không tham gia kỳ mới; Mai chỉ có nhóm EXE201 đã học xong.

### 2.5 Tạo lớp và nhóm ở **kỳ mới**

1. Tạo **một lớp EXE201** (gọi là "lớp EXE201 kỳ mới"). Dùng **Import Students** để nhập danh sách sinh viên của **Nhóm Sao, Nhóm Trăng, Nhóm Mây** (kỳ cũ), đủ **tất cả** thành viên mỗi nhóm. **Không nhập sinh viên của Nhóm Gió.**
   → Hệ thống tự tạo 3 nhóm tiếp tục. Kiểm tra ở **Admin Dashboard**, thẻ **Team Continuity**: phải thấy 3 nhóm được kế thừa.
   Nếu màn hình nhập hiện cảnh báo vàng "Not eligible … Only N member(s) are available", nghĩa là chỉ N thành viên cũ của nhóm đó có trong file, nên chưa đủ 4 người để kế thừa. Hãy bổ sung đúng các sinh viên còn thiếu rồi nhập lại.
2. Trong lớp EXE201 kỳ mới, tạo thêm **Nhóm Mới 4** (sinh viên mới, không từ nhóm cũ nào).
3. Tạo **một lớp EXE101** (gọi là "lớp EXE101 kỳ mới") với **3 nhóm mới**: **Nhóm Mới 1**, **Nhóm Mới 2**, **Nhóm Mới 3** (sinh viên mới).
4. Hai lớp của kỳ mới phải ở trạng thái **Active**, các nhóm cũng Active, thì mới xuất hiện trong bản xem trước.

### 2.6 Kiểm tra dữ liệu trước khi test

- [ ] Kỳ mới: Admin Dashboard → **Team Continuity** hiện 3 nhóm tiếp tục (kế thừa từ Sao, Trăng, Mây).
- [ ] Kỳ mới: danh sách mentor (Teaching staff directory) có đúng 5 mentor doanh nghiệp (An, Bình, Dũng, Em, Phúc) và 3 mentor giảng viên (Hoa, Mai, Nga) đang tham gia.
- [ ] Kỳ mới: **chưa có** mentor nào được gán cho nhóm nào.
- [ ] Kỳ cũ: trang chi tiết lớp hiện đúng mentor theo bảng ở mục 2.4.

### 2.7 Cách mở màn hình để test (dùng ở hầu hết các kiểm tra)

1. Vào `/admin/subjects`, mở tab **Lecturers & Mentors by Semester**.
2. Ở **Semester** và **Year**, chọn **kỳ mới**.
3. Trong thẻ **Mentor import & assignment**, bấm **Assign mentors**.
4. Bấm **Preview assignment** để mở cửa sổ xem trước.

---

## Phần 3. Bảng theo dõi tổng

Điền kết luận sau khi làm xong từng mục.

| Lượt | Yêu cầu | Kết luận (Đạt / Đạt có điều kiện / Chưa đạt) | Ghi chú |
|---|---|---|---|
| 1 | REQ-01 Mentor tham gia theo từng kỳ | | |
| 2 | REQ-02 Giữ mentor cho nhóm tiếp tục | | |
| 3 | REQ-03 Xử lý mentor của nhóm đã hoàn thành | | |
| 4 | REQ-05 Random và Balanced | | |
| 5 | REQ-06 Quy tắc số lượng mentor | | |
| 6 | REQ-04 Chia vào vị trí thiếu và sửa tay | | |
| 7 | REQ-07 Xem trước và xác nhận | | |
| 8 | REQ-08 Xung đột và thay thế mentor | | |
| 9 | REQ-09 Xuất file Excel | | |
| 10 | Kiểm tra chung | | |

---

## Lượt 1. REQ-01: Mentor tham gia theo từng kỳ

**Yêu cầu của cô:** mentor được quản lý trong một danh sách chung. Mỗi kỳ, Admin chọn mentor nào tham gia. Đổi ở kỳ này không ảnh hưởng kỳ khác, và chỉ mentor tham gia kỳ này mới được dùng để chia.

**Dữ liệu khi bắt đầu:** vừa dựng xong ở Phần 2. **Ghi dữ liệu:** không.

### Kiểm tra 1.1. Mỗi kỳ có danh sách riêng

**Làm gì**
1. Ở Teaching staff directory, chọn **kỳ mới**. Tìm mentor Cường và Lan.
2. Đổi sang **kỳ cũ**. Tìm lại Cường và Lan.

**Phải thấy gì**
- [ ] Kỳ mới: Cường và Lan **không** ở trạng thái đang tham gia.
- [ ] Kỳ cũ: Cường và Lan vẫn tham gia, các phân công cũ vẫn như cũ.
- [ ] Mỗi mentor có nhãn loại đúng: **Industry mentor** (màu cam) cho mentor doanh nghiệp, **Lecturer mentor** (màu xanh lá) cho mentor giảng viên.
- [ ] Bộ lọc Role có "All mentors", "Industry mentors", "Lecturer mentors" và lọc đúng.

### Kiểm tra 1.2. Mentor không tham gia kỳ này thì không được dùng

**Làm gì**
1. Ở kỳ mới, bấm **Preview assignment**.
2. Mở tab **By mentor**.
3. Mở tab **By team**, bấm **Change** ở một vị trí bất kỳ, xem danh sách mentor có thể chọn, rồi bấm **Cancel** (chưa đổi gì).

**Phải thấy gì**
- [ ] Cường và Lan **không có** trong bảng By mentor.
- [ ] Cường và Lan **không có** trong danh sách chọn của nút Change.
- [ ] Vị trí mentor doanh nghiệp chỉ liệt kê mentor doanh nghiệp; vị trí mentor giảng viên chỉ liệt kê mentor giảng viên.

### Kiểm tra 1.3. Thêm nhiều mentor vào kỳ cùng lúc

**Làm gì**
1. Ở Teaching staff directory của kỳ mới, bấm **Add mentor**.
2. Trong hộp thoại **Add mentors to <kỳ>**: gõ vài chữ vào ô tìm kiếm, bấm các nút lọc **Industry mentors** và **Lecturer mentors**.
3. Tick vài mentor, hoặc bấm **Select all**, rồi bấm **Add N mentors**.
4. Làm tương tự với **Add lecturer**.

**Phải thấy gì**
- [ ] Danh sách hiển thị đủ tài khoản mentor đang hoạt động, mỗi người kèm nhãn **Industry mentor** hoặc **Lecturer mentor** (và loại hợp đồng nếu có).
- [ ] Ô tìm kiếm lọc theo tên, email và loại hợp đồng, không phân biệt dấu. Nút lọc chỉ giữ đúng loại mentor.
- [ ] Mentor đã ở trong kỳ nằm ở mục **Already in <kỳ>**, mờ và không tick được. Dòng đếm cho biết "N available · M already in …".
- [ ] **Select all** chọn tất cả người đang hiện (khi đang lọc thì ghi "Select all shown"). Nút cuối ghi đúng số người, ví dụ "Add 5 mentors".
- [ ] Sau khi bấm, thông báo "Added 5 mentors." xuất hiện, các mentor vừa thêm chuyển xuống mục "Already in …", và danh sách phía sau (Teaching staff directory) cập nhật ngay.
- [ ] Người nào bị bỏ qua (ví dụ vừa có người khác thêm trước, hoặc đã nằm trong kỳ ở trạng thái Inactive) được nêu tên kèm lý do; những người còn lại vẫn được thêm.
- [ ] Add lecturer hoạt động tương tự, và danh sách chỉ có giảng viên.

**Kết luận REQ-01:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………

---

## Lượt 2. REQ-02: Giữ mentor cho nhóm tiếp tục

**Yêu cầu của cô:** nếu nhóm kỳ trước tiếp tục sang kỳ này, và mentor cũ vẫn tham gia kỳ này, và vị trí đó còn trống, thì hệ thống **đề xuất giữ nguyên** mentor cũ. Nhóm tiếp tục được nhận ra qua liên kết kế thừa của nhóm, **không** dựa vào tên nhóm. Admin vẫn sửa tay được.

**Dữ liệu khi bắt đầu:** như Lượt 1 (kỳ mới chưa gán mentor nào). **Ghi dữ liệu:** không.

Mở **Preview assignment** (đang chọn **Balanced**), vào tab **By team** và tìm 3 nhóm tiếp tục (tên dạng "Nhóm Sao (…)", "Nhóm Trăng (…)", "Nhóm Mây (…)").

### Kiểm tra 2.1. Nhóm tiếp tục giữ mentor cũ

**Phải thấy gì**

| Nhóm tiếp tục | Mentor doanh nghiệp | Mentor giảng viên |
|---|---|---|
| Nhóm Sao | **An**, nhãn Kept | **Hoa**, nhãn Kept |
| Nhóm Trăng | **Bình**, nhãn Kept | **Không phải Lan**, nhãn New (Lan không tham gia kỳ mới) |
| Nhóm Mây | **Không phải Cường**, nhãn New (Cường không tham gia kỳ mới) | **Hoa**, nhãn Kept |

- [ ] Bảng trên đúng cho cả ba nhóm.
- [ ] Hoa được giữ ở **cả hai** nhóm (Sao và Mây): mọi nhóm tiếp tục của một mentor đều được giữ, không phải chọn từng nhóm.
- [ ] Hai vị trí của cùng một nhóm được xử lý riêng (một vị trí giữ, một vị trí mới là bình thường).
- [ ] Ô số liệu **Kept** ở đầu cửa sổ bằng **4**.

### Kiểm tra 2.2. Chỉ trùng tên thì không được giữ mentor

**Làm gì**
1. Đóng cửa sổ xem trước. Trong lớp EXE201 kỳ mới, tạo thêm một nhóm tên **đúng như nhóm cũ "Nhóm Gió"**.
2. Mở lại xem trước.

**Phải thấy gì**
- [ ] Nhóm vừa tạo **không** nhận Dũng hay Hoa tự động. Nó được chia mentor như nhóm mới (nhãn New).

### Kiểm tra 2.3. Đổi tên nhóm tiếp tục thì vẫn được giữ mentor

**Làm gì**
1. Đóng cửa sổ xem trước. Đổi tên nhóm tiếp tục của "Nhóm Sao" sang tên khác (nếu giao diện cho phép).
2. Mở lại xem trước.

**Phải thấy gì**
- [ ] Nhóm đó vẫn giữ An và Hoa (nhãn Kept). Nếu giao diện không cho đổi tên nhóm, ghi **Không áp dụng**.

### Kiểm tra 2.4. Đây chỉ là đề xuất, Admin sửa được

- [ ] Xem Kiểm tra 4.3 (sửa tay một ô nhãn Kept). Tick ở đây nếu kiểm tra đó đạt.

**Kết luận REQ-02:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………

---

## Lượt 3. REQ-03: Xử lý mentor của nhóm đã hoàn thành

**Yêu cầu của cô:** nhóm EXE201 của kỳ trước **kết thúc**, không kế thừa. Mentor của nhóm đã hoàn thành vẫn nằm trong danh sách để chia cho nhóm mới. Mentor cũ không tham gia kỳ mới thì hiển thị **mờ** và không ghép lại, để Admin biết phải phân mentor mới. Mentor có nhiều nhóm thì chỉ giữ những nhóm tiếp tục.

**Dữ liệu khi bắt đầu:** như Lượt 2. **Ghi dữ liệu:** không.

### Kiểm tra 3.1. Nhóm EXE201 của kỳ trước không đi theo mentor

**Làm gì:** mở xem trước, tab **By mentor**, xem dòng của **An**, **Hoa**, **Bình**, **Mai**.

**Phải thấy gì**
- [ ] Cột **Total** của tất cả các mentor, **trước khi chia**, bằng **0** (kỳ mới chưa gán gì; không mang theo Nhóm Núi hay Nhóm Sông).
- [ ] Sau khi chia, số nhóm của An chỉ gồm Nhóm Sao tiếp tục cộng nhóm mới nếu có. **Không** có Nhóm Núi.
- [ ] **Mai** (chỉ từng hướng dẫn Nhóm Sông của EXE201) vẫn có trong danh sách và sẵn sàng nhận nhóm mới.
- [ ] Ở trang chi tiết lớp của kỳ cũ, lịch sử mentor cũ vẫn còn nguyên.

### Kiểm tra 3.2. Mentor không tham gia kỳ mới: hiển thị mờ, kèm lý do

**Làm gì:** mở tab **Needs attention**, tìm mục **Previous mentors not carried over**.

**Phải thấy gì**
- [ ] **Lan** (của Nhóm Trăng) và **Cường** (của Nhóm Mây) hiển thị **mờ**, kèm nhãn **Not active this semester** và dòng chữ cho biết cần phân mentor mới.
- [ ] Hai vị trí đó được chọn mentor mới (đã thấy ở Kiểm tra 2.1).

### Kiểm tra 3.3. Nhóm không tiếp tục thì không giữ mentor, mentor vẫn dùng được

**Phải thấy gì** (cũng ở tab **Needs attention**)
- [ ] **Dũng** và **Hoa** (của Nhóm Gió) hiển thị mờ, kèm nhãn **Team does not continue**.
- [ ] Dũng vẫn có trong tab **By mentor** (vẫn tham gia kỳ mới, sẵn sàng nhận nhóm khác).
- [ ] Không có nhóm nào ở kỳ mới bị gắn mentor của Nhóm Gió chỉ vì cùng tên.

### Kiểm tra 3.4. Mentor có nhiều nhóm: chỉ giữ nhóm tiếp tục

**Phải thấy gì** (Hoa từng hướng dẫn Sao, Mây, Gió và Núi)
- [ ] Hoa được giữ ở **Sao** và **Mây** (nhãn Kept).
- [ ] Hoa **không** được giữ cho Gió (không tiếp tục) và Núi (nhóm EXE201 đã hoàn thành).

### Kiểm tra 3.5. Mentor chưa có nhóm ở trạng thái chưa phân công

**Làm gì:** mở **By mentor**, xem **Mai, Dũng, Em, Phúc, Nga**.

**Phải thấy gì**
- [ ] Cột **Total** trước khi chia của 5 người này bằng **0**.
- [ ] (Tùy chọn) Chọn **Random** và bấm **Preview assignment** vài lần. Nếu có lần nào một mentor kết thúc với tổng bằng 0 thì dòng đó có nhãn **Unassigned** (màu vàng). Nếu không thấy, ghi **Không áp dụng** vì số mentor ít hơn số vị trí.

**Kết luận REQ-03:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………

---

## Lượt 4. REQ-05: Hai cách chia, Random và Balanced

**Yêu cầu của cô:** có hai cách chia. **Balanced** (mặc định) ưu tiên mentor đang có ít nhóm hơn để cân bằng số nhóm; khi nhiều mentor bằng nhau thì chọn ngẫu nhiên. **Random** chọn ngẫu nhiên trong các mentor phù hợp.

**Dữ liệu khi bắt đầu:** như Lượt 3. **Ghi dữ liệu:** không.

### Kiểm tra 5.1. Balanced là mặc định và cân bằng tải

**Làm gì:** mở bảng **Assign mentors**, bấm **Preview assignment**.

**Phải thấy gì**
- [ ] Nút **Balanced (recommended)** đã được chọn sẵn; bên dưới có dòng giải thích "chia cho mentor có ít nhóm nhất".
- [ ] Ở tab **By team**, vị trí mentor doanh nghiệp chỉ có mentor doanh nghiệp, vị trí mentor giảng viên chỉ có mentor giảng viên.
- [ ] Ở tab **By mentor**, những mentor chưa có nhóm (Dũng, Em, Phúc, Mai, Nga) được chọn **trước** những mentor đã giữ nhóm (An, Bình, Hoa).
- [ ] Trong cùng một loại mentor, hai người bất kỳ có số nhóm sau khi chia **chênh nhau không quá 1**.
- [ ] Số nhóm của mentor đã tính cả nhóm được giữ (Kept). Ví dụ Hoa đã có 2 nhóm Kept trước khi nhận thêm.

### Kiểm tra 5.2. Random vẫn tuân thủ quy tắc

**Làm gì:** chọn **Random**, bấm **Preview assignment**, xem kết quả. Bấm **Regenerate** vài lần.

**Phải thấy gì**
- [ ] Vẫn đúng loại mentor, không đè mentor đang có, chỉ dùng mentor đang tham gia kỳ này.
- [ ] Các nhóm có nhãn **Kept** vẫn giữ nguyên mentor cũ (Random chỉ chọn cho chỗ trống).
- [ ] Mỗi lần **Regenerate** thường cho kết quả khác nhau. Đầu cửa sổ có ghi **Strategy** và **seed**.

### Kiểm tra 5.3. Kết quả lặp lại được khi sửa tay

**Làm gì:** ở một bản xem trước, sửa tay một ô (làm như Kiểm tra 4.3), rồi nhìn các nhóm khác.

**Phải thấy gì**
- [ ] Khi bạn sửa một ô, **các nhóm khác không bị đổi** (vì dùng cùng seed).
- Xong kiểm tra này, **chọn lại Balanced** và bấm **Undo** các ô đã sửa.

**Kết luận REQ-05:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………

---

## Lượt 5. REQ-06: Quy tắc số lượng mentor

**Yêu cầu của cô:** một mentor đảm nhiệm nhiều nhóm, không giới hạn cứng. Mỗi nhóm tối đa một mentor doanh nghiệp và một mentor giảng viên. Một người không được chiếm hai vị trí trong cùng một nhóm.

**Dữ liệu khi bắt đầu:** như Lượt 4. **Ghi dữ liệu:** không (có đổi tạm rồi trả lại).

### Kiểm tra 6.1. Một mentor hướng dẫn nhiều nhóm, không bị chặn

**Làm gì:** tạm cho **chỉ một** mentor doanh nghiệp tham gia kỳ mới (bỏ các mentor doanh nghiệp còn lại khỏi kỳ), rồi mở xem trước.

**Phải thấy gì**
- [ ] Mentor doanh nghiệp duy nhất nhận **nhiều nhóm**, không có lỗi hay cảnh báo chặn.
- [ ] Tab **By mentor** hiển thị đúng số nhóm của mentor đó (mức tải để Admin theo dõi).
- **Làm xong, cho các mentor tham gia lại đúng như bảng ở mục 2.3.**

### Kiểm tra 6.2. Mỗi nhóm chỉ một mentor mỗi loại

**Làm gì:** mở tab **By team**, nhìn kỹ từng nhóm.

**Phải thấy gì**
- [ ] Mỗi nhóm có đúng **hai ô**: một mentor doanh nghiệp, một mentor giảng viên. Không nhóm nào có hai mentor cùng loại.
- [ ] Danh sách chọn của **Change** ở mỗi ô chỉ gồm mentor đúng loại của ô đó.
- (Việc "không gán một mentor hai lần vào cùng một nhóm" đã có kiểm thử tự động, không cần làm thủ công.)

**Kết luận REQ-06:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………

---

## Lượt 6. REQ-04: Chia vào vị trí thiếu và cho phép sửa tay

**Yêu cầu của cô:** hệ thống chỉ chia vào những vị trí còn thiếu, đúng loại mentor. Nhóm đã đủ hai mentor thì không đưa vào danh sách chia. Hệ thống **không tự thay thế** mentor đang có. Admin sửa tay được.

### 6.0 Chuẩn bị (đây là phần ghi dữ liệu)

Vào trang chi tiết lớp, khối Mentors, nút **Manage**, gán tay:

| Lớp | Nhóm | Vị trí | Mentor |
|---|---|---|---|
| EXE101 kỳ mới | Nhóm Mới 1 | doanh nghiệp | **Phúc** |
| EXE101 kỳ mới | Nhóm Mới 1 | giảng viên | **Nga** |
| EXE201 kỳ mới | nhóm tiếp tục của Nhóm Trăng | doanh nghiệp | **Em** |

(Nhóm tiếp tục của Nhóm Trăng lẽ ra được giữ mentor Bình. Việc gán tay Em trước tạo ra tình huống "đã có mentor khác ở chỗ đó".)

### Kiểm tra 4.1. Chỉ chia vào chỗ trống

**Làm gì:** mở xem trước, vào tab **By team**.

**Phải thấy gì**
- [ ] **Nhóm Mới 1** có cả hai ô nhãn **Current** (Phúc và Nga). Hệ thống không đề xuất thêm gì cho nhóm này.
- [ ] Các nhóm khác chỉ được điền vào ô còn trống. Ô đã có mentor không bị đổi.
- [ ] Mọi ô mentor doanh nghiệp là mentor doanh nghiệp, mọi ô mentor giảng viên là mentor giảng viên.

### Kiểm tra 4.2. Không tự thay thế mentor đang có

**Phải thấy gì** (cùng bản xem trước)
- [ ] Nhóm tiếp tục của Nhóm Trăng hiển thị **Em** với nhãn **Current**. **Bình không được đặt vào** (hệ thống không ghi đè).
- [ ] Tab **Needs attention**: dòng **Bình** hiển thị mờ, nhãn **Slot already filled**, nghĩa là mentor đang có được giữ nguyên.

### Kiểm tra 4.3. Sửa tay một vị trí

**Làm gì**
1. Ở tab **By team**, tìm một ô mentor doanh nghiệp đang có nhãn **New**. Bấm **Change**.
2. Chọn một mentor khác trong danh sách, bấm **Apply**.

**Phải thấy gì**
- [ ] Ô đổi sang mentor bạn chọn, nhãn **Manual**.
- [ ] Ô số liệu **Manual** tăng thêm 1. Ở tab **By mentor**, số nhóm của hai mentor liên quan được cập nhật.
- [ ] Bấm **Undo** thì ô trở lại đề xuất ban đầu.
- [ ] Sửa tay cả một ô đang là **Kept** (ví dụ đổi An sang người khác) cũng được: ô thành **Manual** và mentor cũ không còn được giữ.
- Sau khi thử xong, bấm **Undo** các ô đã sửa.

### Kiểm tra 4.4. Cố ý để trống một vị trí

**Làm gì**
1. Bấm **Change** ở một vị trí đang có nhãn **New**, chọn **Leave empty**, bấm **Apply**.
2. **Ghi nhớ** nhóm và vị trí này. **Chưa bấm lưu.**

**Phải thấy gì**
- [ ] Vị trí đó chuyển sang nhãn **Missing** và nằm trong tab **Needs attention**.
- [ ] Hệ thống không tự điền lại vị trí đó khi bạn sửa các ô khác.

### Kiểm tra 4.5. Thiếu hẳn một loại mentor vẫn lưu được phần còn lại

**Làm gì:** tạm bỏ toàn bộ mentor giảng viên khỏi kỳ mới, rồi mở xem trước. **Chưa bấm lưu.**

**Phải thấy gì**
- [ ] Hiện cảnh báo vàng nói không có mentor giảng viên nào tham gia.
- [ ] Ô **Missing lecturer** lớn hơn 0. Tab **Needs attention** liệt kê các vị trí còn thiếu (nền đỏ).
- [ ] Nút **Confirm and save** **vẫn bấm được** (chỉ cần thấy nút sáng, không cần bấm).
- **Làm xong, cho các mentor giảng viên tham gia lại như bảng ở mục 2.3, rồi làm lại Kiểm tra 4.4.**

**Kết luận REQ-04:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………
(Phần "sửa tay sau khi đã lưu" được kiểm tra thêm ở Lượt 8 và ở Kiểm tra chung 10.2.)

---

## Lượt 7. REQ-07: Xem trước và xác nhận trước khi lưu

**Yêu cầu của cô:** trước khi lưu phải có bản xem trước (mentor giữ lại, mentor chưa phân công, nhóm còn thiếu mentor doanh nghiệp hoặc giảng viên, kết quả dự kiến, trường hợp không kế thừa được và lý do). Admin kiểm tra rồi mới xác nhận lưu. Chưa xác nhận thì dữ liệu chính thức không đổi.

**Dữ liệu khi bắt đầu:** sau Lượt 6 (đã gán tay 3 mentor, có một vị trí cố ý để trống). **Ghi dữ liệu:** Kiểm tra 7.1 đến 7.3 không ghi; **Kiểm tra 7.4 lưu thật (LƯU LẦN 1).**

### Kiểm tra 7.1. Bản xem trước đủ thông tin

**Phải thấy gì**
- [ ] Đầu cửa sổ có các ô số liệu: **Teams, Kept, New, Manual, Replaced, Missing industry, Missing lecturer**.
- [ ] Tab **By team** có ô tìm kiếm và bộ lọc ("All teams", "Changed by this preview", "Missing a mentor") hoạt động.
- [ ] Tab **By mentor** có cột cho từng môn (EXE101, EXE201), cột **Total** dạng "số trước → số sau", loại hợp đồng của mentor doanh nghiệp.
- [ ] Tab **Needs attention** có các mục Conflicts, Previous mentors not carried over, Slots still without a mentor (khi có dữ liệu tương ứng).

### Kiểm tra 7.2. Xem trước hoặc hủy thì không lưu gì

**Làm gì**
1. Mở xem trước, sửa vài ô, rồi bấm **Cancel** hoặc nút đóng.
2. Mở trang chi tiết hai lớp kỳ mới (khối Mentors) và Teaching staff directory.

**Phải thấy gì**
- [ ] Không có phân công mới nào được tạo, ngoài 3 phân công bạn gán tay ở mục 6.0.
- [ ] Mở lại xem trước thì các chỉnh sửa lúc nãy đã mất. Làm lại Kiểm tra 4.4 một lần nữa (để một vị trí vẫn trống).

### Kiểm tra 7.3. Dữ liệu bị đổi sau khi xem trước thì không cho lưu

**Làm gì**
1. **Tab 1:** mở xem trước và để nguyên đó.
2. **Tab 2:** vào lớp EXE101 kỳ mới, dùng **Manage** gán tay mentor doanh nghiệp **Dũng** cho **Nhóm Mới 2**.
3. **Tab 1:** bấm **Confirm and save**.

**Phải thấy gì**
- [ ] Hiện thông báo lỗi và **thanh đỏ** nói dữ liệu đã thay đổi sau khi tạo bản xem trước.
- [ ] Nút **Confirm and save** bị khóa cho tới khi bấm **Regenerate**.
- [ ] Không có gì của bản xem trước cũ bị lưu.
- [ ] Sau **Regenerate**, Dũng ở Nhóm Mới 2 hiển thị nhãn **Current**.
- Sau bước này, làm lại Kiểm tra 4.4 (để một vị trí vẫn trống) trước khi sang 7.4.

### Kiểm tra 7.4. Lưu đúng như bản xem trước, bấm hai lần không bị trùng (**LƯU LẦN 1**)

**Làm gì**
1. Mở xem trước (Balanced), đảm bảo một vị trí đang để trống (Kiểm tra 4.4). **Chụp ảnh** mentor của vài nhóm ở tab **By team**.
2. Bấm **Confirm and save** **hai lần thật nhanh** (hoặc nhấp đúp).

**Phải thấy gì**
- [ ] Nút chuyển sang trạng thái đang xử lý, chỉ **một** lần lưu thành công, có thông báo nêu số vị trí đã gán.
- [ ] Ở trang chi tiết các lớp kỳ mới (khối Mentors), mentor của từng nhóm **giống đúng ảnh đã chụp**.
- [ ] Mỗi nhóm chỉ có **một** mentor doanh nghiệp và **một** mentor giảng viên, không có dòng bị lặp.
- [ ] **Em** vẫn là mentor doanh nghiệp của nhóm tiếp tục của Nhóm Trăng (không bị ghi đè). Phúc và Nga vẫn ở Nhóm Mới 1.
- [ ] Vị trí để trống **vẫn trống** (hệ thống không tự điền lại).
- [ ] Teaching staff directory cập nhật số nhóm của mentor.
- [ ] Mở xem trước lần nữa: các vị trí đã lưu hiện nhãn **Current**.

**Kết luận REQ-07:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………

---

## Lượt 8. REQ-08: Xung đột và thay thế mentor

**Yêu cầu của cô:** nếu nhóm đã có mentor khác ở cùng vị trí, hệ thống **không tự ghi đè**, hiển thị **cảnh báo** để Admin kiểm tra, và cho Admin chọn **giữ nguyên** hoặc **kết thúc phân công cũ rồi chọn mentor khác**.

**Dữ liệu khi bắt đầu:** sau **LƯU LẦN 1** (các vị trí đã lưu hiện nhãn Current). **Ghi dữ liệu:** Kiểm tra 8.4 lưu thật (**LƯU LẦN 2**).

### Kiểm tra 8.1. Cảnh báo xung đột và giữ nguyên mentor hiện tại

- [ ] Dùng lại kết quả **Kiểm tra 4.2** (Em là Current, Bình không đè, cảnh báo "Slot already filled") và **Kiểm tra 7.4** (Em vẫn ở lại sau khi lưu). Tick ở đây nếu cả hai đạt.

### Kiểm tra 8.2. Thay mentor bắt buộc có lý do

**Làm gì**
1. Mở xem trước. Ở tab **By team**, bấm **Change** vào ô của **Em** (nhãn **Current**).
2. Chọn một mentor doanh nghiệp khác (ví dụ **Phúc**).
3. Thử để trống ô **lý do**, rồi thử gõ 1 đến 2 ký tự.
4. Gõ lý do từ 3 ký tự trở lên (ví dụ "Mentor bận"), bấm **Replace**.

**Phải thấy gì**
- [ ] Nếu lý do trống hoặc dưới 3 ký tự, nút **Replace** **bị khóa**.
- [ ] Sau khi bấm **Replace**: ô hiện mentor mới với nhãn **Manual**, kèm dòng "Replaces Em (assignment ends on save)".
- [ ] Ô số liệu **Replaced** bằng 1.
- [ ] Ở tab **By mentor**, tổng của Em giảm tương ứng (dạng "trước → sau").

### Kiểm tra 8.3. Hủy ở bước xác nhận thay thế

**Làm gì:** bấm **Confirm and save**. Khi hộp thoại **Replace current mentors?** hiện ra, bấm **Cancel**.

**Phải thấy gì**
- [ ] Không có gì được lưu. Cửa sổ xem trước vẫn mở và chỉnh sửa còn nguyên.

### Kiểm tra 8.4. Lưu có thay thế và hậu quả (**LƯU LẦN 2**)

**Làm gì:** bấm **Confirm and save** lần nữa; ở hộp thoại **Replace current mentors?** bấm **Replace and save**.

**Phải thấy gì**
- [ ] Hộp thoại cho biết có bao nhiêu phân công sẽ kết thúc.
- [ ] Sau khi lưu, thông báo nêu cả số phân công đã kết thúc.
- [ ] Ở trang chi tiết lớp: nhóm đó chỉ còn **một** mentor doanh nghiệp đang hoạt động (người mới). Em không còn là mentor đang hoạt động.
- [ ] (Nếu có tài khoản của Em) Đăng nhập bằng tài khoản Em: **không còn thấy** nhóm đó.
- [ ] Lịch sử phân công cũ không bị xóa.

**Kết luận REQ-08:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………

---

## Lượt 9. REQ-09: Xuất file Excel

**Yêu cầu của cô:** sau khi phân công xong, Admin xuất file `.xlsx` **theo học kỳ**, cho **tất cả các lớp**, gồm 3 sheet: **EXE101**, **EXE201** (mỗi dòng một sinh viên) và **Tổng hợp** (số nhóm của từng mentor).

**Dữ liệu khi bắt đầu:** sau Lượt 8. **Ghi dữ liệu:** không. Mở file bằng Excel hoặc phần mềm đọc `.xlsx`.

### Kiểm tra 9.1. File của kỳ mới

**Làm gì:** ở thẻ **Mentor import & assignment**, bấm **Export assignments**.

**Phải thấy gì**
- [ ] Tải về file tên dạng `<mã kỳ>_mentor_assignments.xlsx`.
- [ ] File có đúng **3 sheet**, theo thứ tự: **EXE101**, **EXE201**, **Tổng hợp**.
- [ ] Hai sheet EXE101 và EXE201 có đúng 11 cột: RollNumber, Fullname, Chuyên ngành, SubjectCode, GroupName, Group (kèm mã kỳ), Project Name, Description, Zalo Link, Mentor, Mentor - GV.
- [ ] Mỗi dòng là **một sinh viên**. Tên dự án, mô tả, link Zalo và hai mentor chỉ nằm ở **dòng đầu của mỗi nhóm**. Link Zalo bấm được.
- [ ] Sheet EXE101 chỉ có sinh viên nhóm EXE101. Sheet EXE201 chỉ có sinh viên nhóm EXE201. Có **đủ mọi lớp** của kỳ.
- [ ] Vị trí bạn cố ý để trống ở Kiểm tra 4.4 hiển thị **"Chưa phân công"** trong ô mentor tương ứng.

### Kiểm tra 9.2. Sheet "Tổng hợp"

**Phải thấy gì**
- [ ] Bảng thứ nhất (mentor doanh nghiệp) có các cột: STT, Họ và tên, Nhóm EXE101, Nhóm EXE201, Tổng, Loại HĐ (Thỉnh giảng hoặc Khoán). Cuối bảng có dòng **TỔNG**.
- [ ] Bảng thứ hai có dòng tiêu đề **GIẢNG VIÊN IT** (nền vàng), các cột tương tự nhưng không có Loại HĐ, cuối bảng có dòng **TỔNG**.
- [ ] Chọn vài mentor, **đếm tay** số lần họ xuất hiện ở hai sheet đầu: khớp với số trong bảng.
- [ ] Mentor đang tham gia nhưng chưa có nhóm vẫn có dòng, số 0.
- [ ] Vì còn một vị trí để trống (Kiểm tra 4.4), dòng TỔNG của hai bảng **lệch nhau đúng 1** ở loại mentor bị thiếu. (Nếu mọi nhóm đã đủ hai mentor thì hai dòng TỔNG bằng nhau.)

### Kiểm tra 9.3. File chỉ phản ánh phân công đang có hiệu lực

**Phải thấy gì** (sau Kiểm tra 8.4)
- [ ] **Em không xuất hiện** ở nhóm tiếp tục của Nhóm Trăng. Mentor thay thế có mặt.
- [ ] File kỳ mới không chứa nhóm, sinh viên hay mentor của kỳ cũ.

### Kiểm tra 9.4. File của kỳ cũ

**Làm gì:** đổi sang **kỳ cũ**, bấm **Export assignments**.

**Phải thấy gì**
- [ ] Các nhóm EXE101 (Sao, Trăng, Mây, Gió) và EXE201 (Núi, Sông) hiển thị đúng mentor đã gán.
- [ ] Sheet Tổng hợp đúng: ví dụ An có Nhóm EXE101 = 1 (Nhóm Sao) và Nhóm EXE201 = 1 (Nhóm Núi); Hoa có Nhóm EXE101 = 3 (Sao, Mây, Gió) và Nhóm EXE201 = 1 (Núi).

### Kiểm tra 9.5. Kỳ chưa có lớp

**Làm gì:** chọn một kỳ chưa có lớp EXE101 và EXE201, bấm **Export assignments**.

**Phải thấy gì**
- [ ] File vẫn tải được, có 3 sheet. Hai sheet đầu chỉ có dòng tiêu đề, không báo lỗi.

**Kết luận REQ-09:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………

---

## Lượt 10. Kiểm tra chung

Các kiểm tra này không thuộc riêng yêu cầu nào nhưng cần đạt để tính năng dùng được an toàn.

### Kiểm tra 10.1. Chỉ Admin được dùng

**Làm gì**
1. Đăng nhập bằng tài khoản **giảng viên**, thử mở `/admin/subjects`.
2. Đăng xuất, thử mở `/admin/subjects` khi chưa đăng nhập.

**Phải thấy gì**
- [ ] Giảng viên bị chuyển đi hoặc từ chối, không thấy thẻ **Mentor import & assignment**.
- [ ] Chưa đăng nhập thì bị đưa về trang đăng nhập.

### Kiểm tra 10.2. Giảng viên quản lý mentor ở lớp của mình (hộp thoại Manage)

**Làm gì:** đăng nhập bằng giảng viên là giảng viên chính của lớp EXE201 kỳ mới, mở lớp, bấm **Manage** ở khối Mentors, tab **Assign new**.
1. Chọn một mentor doanh nghiệp. Cột bên phải liệt kê các nhóm còn thiếu mentor doanh nghiệp.
2. Bấm ô **Select all** để chọn hết, rồi bỏ tick một nhóm. Gõ vào ô tìm kiếm nhóm để thu hẹp danh sách rồi bấm **Select all shown**.
3. Bấm **Assign to N teams**.

**Phải thấy gì**
- [ ] Có thể tick **nhiều nhóm** cho cùng một mentor; nút ghi đúng số nhóm ("Assign to 3 teams").
- [ ] Mỗi nhóm trong danh sách hiện mentor còn lại của nhóm (ví dụ "Lecturer mentor: Hoa" hoặc "No lecturer mentor yet").
- [ ] Sau khi gán, hộp thoại **vẫn mở**, hiện thông báo xanh "Assigned to N teams.". Các nhóm vừa gán biến khỏi danh sách; số "N teams this semester" của mentor tăng.
- [ ] Khối Mentors ở trang lớp phía sau cũng được cập nhật mà không bị tải lại toàn trang.
- [ ] Nếu một nhóm không gán được (ví dụ vừa có người khác gán trước), thông báo vàng nêu rõ nhóm nào và vì sao; các nhóm còn lại vẫn được gán, nhóm lỗi vẫn được tick để thử lại.
- [ ] Tab **Current assignments** có nút **End** (bắt buộc nhập lý do). Sau khi kết thúc, hộp thoại vẫn mở và danh sách cập nhật.
- [ ] Phân công làm ở đây được tính vào số nhóm của mentor khi Admin xem trước lần sau.

### Kiểm tra 10.3. Giao diện sáng, tối và màn hình nhỏ

**Phải thấy gì**
- [ ] Cửa sổ xem trước đọc được ở cả chế độ **sáng** và **tối**: chữ rõ, các nhãn (Current, Kept, New, Manual, Missing) và ô cảnh báo không bị mờ hay lệch màu.
- [ ] Thu nhỏ cửa sổ trình duyệt (khoảng 800 px, rồi khoảng 400 px): bảng kéo ngang được, không vỡ bố cục, các nút vẫn bấm được.
- [ ] Dùng phím **Tab** đi qua các nút, ô chọn và các tab: thứ tự hợp lý, thấy rõ ô đang được chọn.
- [ ] Tab đang xem được tô đậm rõ.

### Kiểm tra 10.4. Khi có lỗi mạng hoặc máy chủ

**Làm gì**
1. Tắt backend (hoặc ngắt mạng), bấm **Preview assignment**.
2. Bật lại backend, mở xem trước, rồi tắt backend, thử bấm **Change → Apply**, sau đó thử **Confirm and save**.

**Phải thấy gì**
- [ ] Mỗi lần đều có thông báo lỗi dễ hiểu, **không** có màn hình trắng hay lỗi kỹ thuật khó đọc.
- [ ] Khi một chỉnh sửa thất bại, cửa sổ vẫn giữ bản xem trước và các chỉnh sửa trước đó.
- [ ] Bật lại backend là thao tác tiếp được.

### Kiểm tra 10.5. Các chức năng cũ vẫn hoạt động

**Phải thấy gì**
- [ ] **Import mentor list** (xem trước rồi lưu) hoạt động như trước.
- [ ] **Reuse mentors**, **Add mentor**, **Add lecturer** hoạt động như trước.
- [ ] Mentor nhập thiếu thông tin vẫn hiện "Needs information" và không được đưa vào chia.
- [ ] Xuất danh sách của **một lớp** (ở trang chi tiết lớp) vẫn ra **một sheet** như trước, ô mentor còn thiếu để trống (không có chữ "Chưa phân công").

**Kết luận phần kiểm tra chung:** ☐ Đạt  ☐ Đạt có điều kiện  ☐ Chưa đạt   Ghi chú: ……………………

---

## Phần 4. Hạn chế đã biết (chỉ ghi nhận, chưa tính là lỗi mới)

### Mở lại lớp đã hoàn thành

**Làm gì:** ở kỳ cũ, bấm **Reopen Class** cho lớp EXE101 (nếu hệ thống cho phép).

**Hiện tượng đã biết**
- [ ] Các phân công mentor đã kết thúc khi hoàn thành lớp **không được khôi phục**. Lớp mở lại không còn mentor. Nếu sau đó chia mentor cho kỳ sau, hệ thống sẽ không thấy mentor của lớp đó cho tới khi bạn gán lại.
- Ghi nhận lại để hỏi cô giáo xem có muốn thay đổi không.

---

## Phần 5. Tổng kết

### 5.1 Khi nào coi là đạt toàn bộ

- Cả 9 yêu cầu (REQ-01 đến REQ-09) có kết luận **Đạt** (hoặc **Đạt có điều kiện** với điểm cần sửa đã ghi rõ và được cô chấp nhận).
- Phần kiểm tra chung đạt.
- Không có lỗi nghiêm trọng: mất dữ liệu, ghi đè mentor ngoài ý muốn, lưu trùng, hay người không phải Admin vào được chức năng này.
- Mọi điểm khác với "Phải thấy gì" đã được ghi lại kèm ảnh chụp.

### 5.2 Khi có yêu cầu chưa đạt

Gửi cho nhóm phát triển: tên yêu cầu và số kiểm tra (ví dụ "REQ-02, Kiểm tra 2.1"), ảnh chụp màn hình, và câu "đã làm gì, thấy gì, đáng lẽ thấy gì". Sau khi sửa, làm lại **chỉ mục của yêu cầu đó**; nếu mục đó nằm sau Lượt 6, có thể cần dựng lại dữ liệu theo Phần 2.
