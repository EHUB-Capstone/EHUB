# Bộ dữ liệu thao tác thử AI proposal

## Dữ liệu lịch sử được seed

| Startup | Chủ đề | Mức liên quan kỳ vọng với SmartStay |
|---|---|---|
| RoomHub | Phòng trọ đã xác minh, lọc theo khoảng cách và ngân sách | Rất cao |
| MateStay | Ghép bạn cùng phòng theo ngân sách và thói quen | Cao |
| SafeDorm | An toàn và xử lý sự cố tại khu trọ | Trung bình |
| MealRescue | Điều phối thực phẩm dư thừa | Thấp |
| CampusGo | Theo dõi xe buýt nội khuôn viên | Thấp |
| StudyMate | Gia sư đồng trang lứa | Thấp |

## Proposal mục tiêu để nhập trên giao diện

Không seed proposal này. Hãy nhập bằng Team Leader của team muốn kiểm thử để hệ thống tạo version và analysis job theo đúng luồng production.

### Proposal title

Nền tảng gợi ý phòng trọ và bạn cùng phòng an toàn cho sinh viên

### Startup name

SmartStay

### Tagline

Tìm đúng phòng, ghép đúng bạn, an tâm sống gần trường

### Problem statement

Sinh viên, đặc biệt là sinh viên năm nhất hoặc người chuyển đến thành phố mới, mất nhiều thời gian tìm phòng trọ gần trường và thường phải lựa chọn từ các bài đăng thiếu kiểm chứng. Thông tin về giá thuê, chi phí phát sinh, khoảng cách, an ninh và tiện ích không minh bạch. Sau khi tìm được phòng, sinh viên vẫn gặp rủi ro chọn bạn cùng phòng không phù hợp về ngân sách, giờ giấc, vệ sinh và thói quen sinh hoạt, dẫn đến mâu thuẫn hoặc phải chuyển chỗ ở sớm.

### Proposed solution

SmartStay xây dựng một nền tảng dành riêng cho sinh viên, kết hợp danh sách phòng trọ đã xác minh với chức năng ghép bạn cùng phòng. Hệ thống cho phép lọc phòng theo khoảng cách đến trường, tổng chi phí, tiện ích và mức độ an toàn. Sinh viên tạo hồ sơ nhu cầu về ngân sách, khu vực, lịch học, giờ ngủ, mức độ sạch sẽ và quy tắc sống chung; thuật toán sử dụng các tiêu chí này để gợi ý phòng và người ở ghép phù hợp. Hai bên có thể xem thông tin, trao đổi và xác nhận trước khi kết nối.

### Target customers

Khách hàng chính là sinh viên đại học đang tìm phòng trọ hoặc bạn ở ghép gần trường. Nhóm khách hàng trả phí bổ sung gồm chủ nhà, đơn vị quản lý phòng trọ và ký túc xá muốn tiếp cận đúng người thuê, xác minh thông tin và quản lý yêu cầu kết nối hiệu quả hơn.

### Value proposition

SmartStay giảm đồng thời hai rủi ro lớn của sinh viên là phòng trọ thiếu tin cậy và bạn cùng phòng không tương thích. Giá trị khác biệt nằm ở việc kết hợp dữ liệu phòng đã xác minh, chi phí minh bạch, tiêu chí sống chung có cấu trúc và gợi ý theo mức độ phù hợp trong cùng một quy trình thay vì buộc sinh viên tìm kiếm qua nhiều nhóm mạng xã hội rời rạc.

### Market size and potential

Thị trường ban đầu là sinh viên tại các trường đại học ở khu vực thành phố, ưu tiên người học từ tỉnh khác đến và có nhu cầu thuê trọ. Nhóm sẽ đo số lượt tìm kiếm phòng, tỷ lệ kết nối chủ nhà, số cặp ở ghép thành công và tỷ lệ duy trì sau ba tháng để ước lượng thị trường thực tế trước khi mở rộng sang các trường và khu vực lân cận.

### Competitors and advantage

Các lựa chọn hiện tại gồm nhóm Facebook, nền tảng đăng tin bất động sản, môi giới và bảng thông báo sinh viên. Những kênh này mạnh về số lượng tin nhưng chưa kết hợp xác minh phòng với ghép bạn ở theo thói quen. SmartStay tập trung vào ngữ cảnh sinh viên, tổng chi phí thực tế, khoảng cách đến trường, xác minh thông tin và khả năng tương thích của người ở ghép.

### Business model

Sinh viên được tìm kiếm cơ bản miễn phí. Nền tảng thu phí tùy chọn cho xác minh danh tính, hồ sơ ưu tiên và báo cáo tương thích chi tiết. Chủ nhà hoặc đơn vị quản lý trả phí cho tin đăng đã xác minh, gói hiển thị ưu tiên và công cụ quản lý yêu cầu. Doanh thu chỉ được mở rộng sau khi pilot chứng minh tỷ lệ kết nối thành công và mức độ tin cậy của dữ liệu.

### Revenue model

Nguồn thu dự kiến gồm phí tin đăng xác minh, thuê bao quản lý theo số phòng, phí kết nối ưu tiên và gói xác minh người dùng. Giai đoạn đầu sử dụng mức giá thử nghiệm, theo dõi chi phí xác minh và chi phí có được một kết nối thành công trước khi chốt bảng giá chính thức.

### Marketing and sales strategy

Nhóm tiếp cận sinh viên thông qua câu lạc bộ, phòng công tác sinh viên, cộng đồng tân sinh viên và chương trình giới thiệu bạn bè. Phía cung được phát triển bằng cách làm việc trực tiếp với chủ nhà quanh trường, hỗ trợ chuẩn hóa thông tin và cấp dấu xác minh. Pilot chỉ triển khai trong bán kính nhỏ để bảo đảm mật độ phòng và chất lượng vận hành.

### Technology

Frontend sử dụng React và TypeScript; backend sử dụng ASP.NET Core, PostgreSQL và background job. Hệ thống dùng bản đồ để tính khoảng cách, cơ chế xác minh nội dung và mô hình chấm điểm có trọng số cho nhu cầu phòng cùng mức độ tương thích của bạn ở. Các tiêu chí và trọng số phải được lưu theo phiên bản để có thể giải thích và kiểm thử.

### Financial plan

Chi phí MVP tập trung vào phát triển phần mềm, lưu trữ hình ảnh, bản đồ, xác minh danh sách phòng và vận hành hỗ trợ. Nhóm đặt giới hạn ngân sách cho mỗi khu vực pilot, theo dõi chi phí xác minh trên mỗi phòng và doanh thu trung bình trên mỗi kết nối. Chỉ mở rộng khi tỷ lệ tin giả, khiếu nại và chi phí hỗ trợ nằm trong ngưỡng chấp nhận.

### Roadmap and milestones

Giai đoạn một khảo sát nhu cầu và chuẩn hóa bộ tiêu chí phòng trọ, bạn cùng phòng. Giai đoạn hai xây dựng MVP, nhập tối thiểu năm mươi phòng thử nghiệm và kiểm thử với một nhóm sinh viên. Giai đoạn ba đo tỷ lệ gợi ý phù hợp, số kết nối thành công và phản hồi sau khi ở. Giai đoạn bốn cải thiện xác minh, bổ sung đánh giá và mở rộng có kiểm soát sang các trường lân cận.

### Team introduction and roles

Team Leader phụ trách sản phẩm và làm việc với nhà trường; thành viên backend xây dựng API, database và background job; thành viên frontend phát triển trải nghiệm tìm kiếm, so sánh và quản lý hồ sơ; thành viên dữ liệu phụ trách tiêu chí ghép cặp, đánh giá chất lượng gợi ý và báo cáo thử nghiệm.

### Version change note

Tạo proposal SmartStay để kiểm thử AI retrieval và báo cáo so sánh với dữ liệu lịch sử local.

## Kết quả kỳ vọng

Khi AI hoàn tất:

1. `RoomHub` nên đứng trong Top 2 vì cùng bài toán tìm phòng, xác minh, khoảng cách và ngân sách.
2. `MateStay` nên đứng trong Top 2 vì cùng bài toán ghép bạn ở theo ngân sách và thói quen.
3. `SafeDorm` có thể đứng thứ ba do cùng bối cảnh lưu trú sinh viên nhưng khác trọng tâm giải pháp.
4. `MealRescue`, `CampusGo` và `StudyMate` không nên vượt cả RoomHub lẫn MateStay.
5. Báo cáo nên nhận ra SmartStay kết hợp hai hướng trước đây tách rời: tìm phòng đã xác minh và ghép bạn ở tương thích.
6. Bằng chứng phải trích từ nội dung thật của SmartStay hoặc proposal lịch sử, không được tự tạo thông tin ngoài snapshot.

Không đặt kỳ vọng vào một tỷ lệ phần trăm cố định. Điểm và thứ tự giữa vị trí 1–2 có thể thay đổi theo embedding provider/model; tiêu chí đạt là hai proposal liên quan nhất nằm trước các proposal đối chứng rõ ràng.
