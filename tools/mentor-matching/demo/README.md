# Kiểm tra model v2 — chỉ tên đề tài và mô tả

Chạy 14 CV hư cấu với 22 dự án giả lập qua reader/parser và handler backend thật.
Bộ cũ: Top 1 khớp 16/16 vị trí thuộc 8 dự án thông thường; ca khó đạt 5/5 (bản trước 2/5).
Bộ mô tả mới: Top 1 khớp 13/13 vị trí có kỳ vọng; 7/7 kiểm tra bổ sung đạt.

Đây là kỳ vọng tác giả trên tình huống tự tạo, không phải độ chính xác thực tế hoặc đánh giá độc lập. Các file demo không được trainer đọc. Bộ mới nằm ngoài curriculum nhưng do cùng tác giả thiết kế; không coi đây là kiểm định mù.

## Đầu vào và phương pháp

Handler chỉ lấy tên đề tài và mô tả (Project.Name/Description, hoặc TeamName/Description khi chưa có nội dung tương ứng). Không đưa Technology, StartupField, Problem, Solution, tag, trường nhu cầu riêng hoặc đáp án kỳ vọng vào model.
12 ca cũ giữ nguyên nội dung: các đoạn nhu cầu trước đây được gộp vào description để kiểm tra hồi quy. 10 ca mới dùng mô tả tự nhiên, bao gồm trường hợp không nêu công nghệ, chỉ có nhu cầu kinh doanh, và mô tả quá chung chung.

Model lọc phủ định rõ ràng tiếng Việt/Anh, giữ 'không chỉ… mà còn…', phân biệt chuyên môn cụ thể với chủ đề cha và ưu tiên cụm dài như React Native. Tag cũ không lấn át câu phủ định trong CV; học vấn vẫn ở Bio nhưng không tự tạo tag chuyên môn khi tải CV mới.
Bộ đọc CV giữ lại câu nghề nghiệp như phát triển ứng dụng trên điện thoại hoặc email marketing cho ngân hàng. Chỉ các nhãn liên hệ tương ứng được xem là thông tin riêng tư; câu nghề nghiệp không bị loại chỉ vì có từ điện thoại/email/ngân hàng.
Ứng viên chỉ trùng từ chung hoặc không khớp chuyên môn bị loại. Mô tả kinh doanh không tự tạo nhu cầu IT từ ngành nghề. Lý do trả về ghi cả phần khớp và chuyên môn chưa tìm thấy bằng chứng.

## So sánh các lỗi trước đây

| Tình huống | Trước | Sau |
| --- | --- | --- |
| P09 — không có chuyên gia lên men | Trả 14 ứng viên | Trả 0 ứng viên |
| P10 — không cần AI, cần .NET | I06 (99); I01 (94); I04 (76) | I01 (100); I06 (98); I05 (38) |
| P05 — logistics nông sản | B03 (76); B08 (76); B05 (26) | B03 (100); B08 (78) |
| P11 — tài chính thực tế | B02 (99); B01 (85); B07 (56) | B02 (100); B07 (35) |

Điểm 0–100 là điểm xếp hạng, không phải xác suất đúng. Không so sánh mức tăng điểm giữa các model như mức tăng độ chính xác. Thứ tự dùng giá trị chưa làm tròn; số nguyên trên UI có thể bằng nhau.

## Danh sách mentor

| ID | Tên hư cấu | Loại | Nội dung chuyên môn |
| --- | --- | --- | --- |
| B01 | Mai (giả lập) | Business | Marketing F&B, nghiên cứu khách hàng, bán hàng và định giá gói sản phẩm. |
| B02 | Nam (giả lập) | Business | Tài chính startup, dòng tiền, ngân sách, gọi vốn và pitch deck. |
| B03 | Linh (giả lập) | Business | Vận hành kho, logistics, chuỗi cung ứng thực phẩm và phân phối nông sản. |
| B04 | Vy (giả lập) | Business | Nghiên cứu thị trường giáo dục, phỏng vấn người dùng và khám phá sản phẩm. |
| B05 | Hà (giả lập) | Business | Bán hàng B2B và phát triển khách hàng doanh nghiệp cho edtech. |
| B06 | Sơn (giả lập) | Business | Pháp lý doanh nghiệp, sở hữu trí tuệ, bản quyền và hợp đồng thương mại. |
| B07 | Minh (giả lập) | Business | Học tài chính, kinh nghiệm làm việc chủ yếu marketing; không phải chuyên gia quản lý vốn. |
| B08 | Phương (giả lập) | Business | Bán tour, marketing du lịch, kênh phân phối và vận hành trải nghiệm địa phương. |
| I01 | Khoa (giả lập) | IT | Backend C#/.NET, PostgreSQL và xử lý đơn hàng thương mại điện tử, đồ ăn. |
| I02 | An (giả lập) | IT | Ứng dụng Flutter, Android/iOS cho giáo dục và du lịch. |
| I03 | Trâm (giả lập) | IT | Frontend React/TypeScript, UX và trải nghiệm sản phẩm số. |
| I04 | Duy (giả lập) | IT | AI, computer vision, machine learning và phân tích dữ liệu nông nghiệp. |
| I05 | Bảo (giả lập) | IT | Cloud, DevOps, triển khai và bảo mật hệ thống. |
| I06 | Quân (giả lập) | IT | Backend Node.js/PostgreSQL; CV ghi rõ không có kinh nghiệm AI hoặc computer vision. |

## 12 tình huống hồi quy

| Đề tài | Mô tả duy nhất đưa vào model | Business Top 3 | IT Top 3 |
| --- | --- | --- | --- |
| P01 — Bếp Lành Sinh Viên — dự án giả lập | Dự án cung cấp suất ăn lành mạnh theo tuần cho sinh viên. Nhóm đã thử bán tại hai khu trọ và đang xây dựng công cụ nhận đơn trực tuyến. Cần mentor có kinh nghiệm F&B, nghiên cứu khách hàng, marketing, bán hàng và định giá để kiểm chứng giá gói ăn và tìm khách hàng đầu tiên. Cần mentor thiết kế backend C#/.NET, PostgreSQL và xử lý đơn hàng thương mại điện tử cho dịch vụ đặt đồ ăn. | B01 (99); B07 (84); B03 (77) | I01 (99); I06 (93); I05 (24) |
| P02 — Bạn Học Nhỏ — dự án giả lập | Dự án hỗ trợ học sinh tự tổ chức lịch học và luyện tập ngắn mỗi ngày. Nhóm mới có bản thiết kế và cần kiểm chứng cách sử dụng trước khi xây sản phẩm đầu tiên. Cần mentor nghiên cứu thị trường giáo dục, phỏng vấn phụ huynh và học sinh, khám phá sản phẩm và kiểm chứng nhu cầu người dùng. Cần mentor phát triển ứng dụng di động giáo dục bằng Flutter cho Android và iOS. | B04 (100); B01 (90); B07 (32) | I02 (99) |
| P03 — Chuyến Đi Bản Địa — dự án giả lập | Dự án kết nối du khách với trải nghiệm địa phương theo nhóm nhỏ. Một số trải nghiệm đã được thử nghiệm và nhóm muốn tổ chức hoạt động ổn định hơn. Cần mentor có kinh nghiệm du lịch, bán tour, marketing du lịch, phát triển kênh phân phối và vận hành dịch vụ trải nghiệm. Cần mentor xây ứng dụng du lịch trên điện thoại bằng Flutter, Android và iOS để khách xem lịch trình và đặt trải nghiệm. | B08 (100); B01 (94); B07 (88) | I02 (100) |
| P04 — Kho Gọn Cửa Hàng — dự án giả lập | Dự án hỗ trợ các cửa hàng nhỏ xử lý hàng hóa và đơn bán trực tuyến. Nhóm đang thử nghiệm với một số cửa hàng có nhiều điểm lưu kho. Cần mentor quản lý vận hành kho, chuỗi cung ứng, logistics, nhập xuất tồn và phân phối hàng hóa để thiết kế quy trình phục vụ cửa hàng. Cần mentor thiết kế backend và cơ sở dữ liệu PostgreSQL cho tồn kho, đơn hàng và thương mại điện tử; nhóm có thể chọn C#/.NET hoặc Node.js. | B03 (100); B08 (77) | I01 (99); I06 (98); I05 (27) |
| P05 — Nông Sản Đúng Chuyến — dự án giả lập | Dự án kết nối nông hộ với điểm bán nông sản. Nhóm đã có mô hình AI dự báo sản lượng và đang thử nghiệm việc đưa sản phẩm tới người mua. Nút thắt hiện tại là thu mua, logistics, vận hành chuỗi cung ứng và phân phối nông sản. Cần mentor giúp giảm hàng hỏng và tổ chức giao nhận. Cần mentor AI, phân tích dữ liệu nông nghiệp và computer vision để đánh giá chất lượng dữ liệu và cải thiện mô hình dự báo, phân loại nông sản hiện có. | B03 (100); B08 (78) | I04 (100) |
| P06 — Mắt Kiểm Hàng — dự án giả lập | Dự án cung cấp sản phẩm số hỗ trợ kiểm tra bề ngoài hàng hóa từ hình ảnh. Nhóm đã có bản thử nghiệm và muốn chuẩn bị giai đoạn thử nghiệm thương mại tiếp theo. Cần mentor tài chính, lập kế hoạch dòng tiền, dự phóng chi phí và chuẩn bị gọi vốn để xác định số vốn cần huy động cho giai đoạn tiếp theo. Cần mentor có kinh nghiệm AI, computer vision, machine learning và phân tích dữ liệu để đánh giá, cải thiện mô hình nhận diện lỗi từ hình ảnh. | B02 (100); B07 (36) | I04 (100) |
| P07 — Bản Quyền Sáng Tạo — dự án giả lập | Dự án cung cấp nền tảng số cho nhóm sáng tạo quản lý và chuyển giao tài sản thiết kế. Nhóm chuẩn bị thử nghiệm với những khách hàng tổ chức đầu tiên. Cần mentor pháp lý, sở hữu trí tuệ, bản quyền và hợp đồng thương mại để rà soát quyền sử dụng tài sản và điều khoản chuyển giao. Cần mentor cloud, DevOps và bảo mật để thiết kế triển khai, phân quyền truy cập, lưu trữ dữ liệu và bảo vệ hệ thống. | B06 (100) | I05 (100); I01 (70); I06 (68) |
| P08 — Cổng Dịch Vụ Đào Tạo — dự án giả lập | Dự án cung cấp dịch vụ số giúp đơn vị đào tạo tổ chức chương trình học cho doanh nghiệp. Nhóm có bản sản phẩm thử nghiệm và đang tìm khách hàng tổ chức trả phí. Cần mentor bán hàng B2B, phát triển khách hàng doanh nghiệp, xây quy trình tiếp cận và chào bán giải pháp edtech cho tổ chức. Cần mentor frontend React, TypeScript, UX và thiết kế trải nghiệm sản phẩm để cải thiện cổng dịch vụ cho người quản trị và học viên. | B05 (96); B01 (85); B04 (80) | I03 (99) |
| P09 — Vật Liệu Lên Men — dự án giả lập | Dự án phát triển vật liệu sinh học bằng quy trình lên men trong phòng thí nghiệm. Nhóm cần chuyển công thức thử nghiệm sang sản xuất vật liệu ở quy mô lớn hơn. Cần chuyên gia có kinh nghiệm trực tiếp về công nghệ lên men, sản xuất vật liệu sinh học, thiết kế dây chuyền và mở rộng quy trình từ phòng thí nghiệm sang nhà máy. Cần chuyên gia kỹ thuật công nghệ sinh học và thiết bị lên men công nghiệp. Hiện không có nhu cầu phát triển phần mềm. | Không gợi ý | Không gợi ý |
| P10 — Đơn Hàng Rõ Ràng — dự án giả lập | Dự án xây hệ thống quản lý đơn hàng cho cửa hàng. Phạm vi phiên bản này KHÔNG cần AI, KHÔNG cần computer vision và KHÔNG cần machine learning. Nhóm chỉ cần hoàn thiện phần mềm quản lý dữ liệu thông thường. Cần mentor backend C#/.NET và PostgreSQL để thiết kế API, đơn hàng và cơ sở dữ liệu. Các chủ đề AI, machine learning và computer vision đều nằm ngoài nhu cầu cố vấn hiện tại. | Không gợi ý | I01 (100); I06 (98); I05 (38) |
| P11 — Kế Hoạch Vốn Ban Đầu — dự án giả lập | Dự án đang chuẩn bị kế hoạch triển khai trong sáu tháng tới. Nhóm cần đánh giá khả năng duy trì hoạt động trước khi quyết định nhận thêm vốn. Cần mentor có kinh nghiệm thực tế quản lý tài chính, dòng tiền, dự phóng ngân sách và gọi vốn để rà soát kế hoạch vốn và thời gian duy trì hoạt động. | B02 (100); B07 (35) | Không gợi ý |
| P12 — Ý Tưởng Mới — dự án giả lập | Thiếu mô tả → validation | Không gợi ý | Không gợi ý |

## 10 mô tả mới ngoài curriculum

| Đề tài | Mô tả duy nhất đưa vào model | Business Top 3 | IT Top 3 |
| --- | --- | --- | --- |
| H01 — Sổ chăm sóc thú cưng — giả lập | Ứng dụng di động bằng Flutter trên Android và iOS giúp chủ nuôi theo dõi lịch chăm sóc thú cưng. Nhóm đang nghiên cứu thị trường và phỏng vấn khách hàng để kiểm chứng nhu cầu trước khi phát hành. | B01 (100); B04 (100); B07 (64) | I02 (100) |
| H02 — Xưởng đồ da tái sử dụng — giả lập | Xưởng đang tính ngân sách, lập dòng tiền và chuẩn bị gọi vốn cho sáu tháng sản xuất tiếp theo. Giai đoạn này không phát triển phần mềm, không cần ứng dụng di động hoặc hệ thống backend. | B02 (100); B07 (35) | Không gợi ý |
| H03 — Kho cấp phép thiết kế — giả lập | Dịch vụ quản lý quyền sử dụng thiết kế và hợp đồng thương mại, cần làm rõ bản quyền và sở hữu trí tuệ. Dữ liệu lưu trên AWS; nhóm đang chuẩn bị triển khai cloud và kiểm tra bảo mật. | B06 (100) | I05 (100) |
| H04 — Cổng AI đơn hàng — giả lập | Phiên bản hiện tại KHÔNG cần AI, computer vision hoặc machine learning. Hệ thống chỉ xử lý đơn hàng bằng C#/.NET và PostgreSQL; không cần tư vấn kinh doanh trong lần đánh giá này. | Không gợi ý | I01 (100); I06 (98); I05 (35) |
| H05 — Lớp học trên điện thoại — giả lập | Ứng dụng React Native cho Android và iOS cung cấp nội dung học ngắn. Nhóm edtech muốn bán hàng B2B cho doanh nghiệp để tổ chức chương trình đào tạo. | B05 (100); B01 (77); B07 (63) | I02 (93) |
| H06 — Động cơ quỹ đạo — giả lập | Dự án nghiên cứu động cơ ion cho vệ tinh, cần chuyên môn vật lý plasma và thử nghiệm buồng chân không trong môi trường quỹ đạo. | Không gợi ý | Không gợi ý |
| H07 — Bữa trưa theo tuần — giả lập | Dịch vụ F&B không chỉ làm marketing mà còn nghiên cứu thị trường, bán hàng và thử nghiệm giá bán suất ăn cho sinh viên. Nhóm chưa có nhu cầu phần mềm hoặc ứng dụng di động. | B01 (100); B07 (86); B08 (78) | Không gợi ý |
| H08 — Ý tưởng kết nối — giả lập | Một nền tảng giúp mọi người kết nối thuận tiện và làm việc hiệu quả hơn. | Không gợi ý | Không gợi ý |
| H09 — Hộp cơm tiện lợi — giả lập | Trang web đặt hàng trực tuyến các suất ăn theo tuần cho người đi làm. Nhóm muốn tìm khách mới và thử các mức giá bán trước khi mở rộng dịch vụ. | B01 (100); B07 (78); B08 (77) | I01 (92); I03 (68); I06 (63) |
| H10 — Ghé thăm làng — giả lập | Ứng dụng trên điện thoại giúp du khách chọn chuyến tham quan làng nghề. Nhóm đang tổ chức hoạt động cùng người dân địa phương và xây kênh phân phối các trải nghiệm. | B08 (100); B03 (95); B01 (80) | I02 (99) |

## Kiểm tra ca khó

| Dự án | Kiểm tra | Kết quả | Quan sát |
| --- | --- | --- | --- |
| P09 | unsupported-specialty | Đạt kỳ vọng mô phỏng | Returned 0 Business and 0 IT candidates; the API does not expose a separate specialty-abstention flag. |
| P10 | negated-project-need | Đạt kỳ vọng mô phỏng | Expected C#/.NET backend mentor I01 first; actual first IT mentor: I01. |
| P10 | negated-cv-expertise | Đạt kỳ vọng mô phỏng | I06 CV explicitly denies AI experience; extracted AI expertise tag: False. |
| P11 | experience-over-degree | Đạt kỳ vọng mô phỏng | B02 finance-work-experience rank: 1; B07 finance-degree-only rank: 2. |
| P12 | missing-project-description | Đạt kỳ vọng mô phỏng | Expected existing description validation; actual success: False; error: COMMON_VALIDATION_ERROR. |
| H02 | business-only-no-it | Đạt kỳ vọng mô phỏng | Business-only description returned 2 Business and 0 IT candidates. |
| H04 | negated-project-need | Đạt kỳ vọng mô phỏng | Expected C#/.NET backend mentor I01 first; actual first IT mentor: I01. |
| H04 | negated-cv-expertise | Đạt kỳ vọng mô phỏng | I06 CV explicitly denies AI experience; extracted AI expertise tag: False. |
| H05 | react-native-not-react-web | Đạt kỳ vọng mô phỏng | Mobile mentor I02 expected; first IT: I02; React web I03 returned: False. |
| H06 | unsupported-specialty | Đạt kỳ vọng mô phỏng | Returned 0 Business and 0 IT candidates; the API does not expose a separate specialty-abstention flag. |
| H07 | business-only-no-it | Đạt kỳ vọng mô phỏng | Business-only description returned 6 Business and 0 IT candidates. |
| H08 | unsupported-specialty | Đạt kỳ vọng mô phỏng | Returned 0 Business and 0 IT candidates; the API does not expose a separate specialty-abstention flag. |

## Ví dụ lý do trả về

**P10 / I01: 100 điểm**

- Kinh nghiệm có nội dung liên quan: backend
- Kinh nghiệm có nội dung liên quan: c#
- Kinh nghiệm có nội dung liên quan: cơ sở dữ liệu
- Kinh nghiệm có nội dung liên quan: postgresql
- Mô hình chưa được kiểm chứng bằng đánh giá ghép mentor thực tế
- Đang phụ trách 0 nhóm

**P10 / I06: 98 điểm**

- Kinh nghiệm có nội dung liên quan: backend
- Kinh nghiệm có nội dung liên quan: cơ sở dữ liệu
- Kinh nghiệm có nội dung liên quan: postgresql
- Chưa tìm thấy bằng chứng trong hồ sơ cho: C# và .NET
- Mô hình chưa được kiểm chứng bằng đánh giá ghép mentor thực tế
- Đang phụ trách 0 nhóm

**H05 / I02: 93 điểm**

- Kinh nghiệm có nội dung liên quan: ứng dụng di động
- Kinh nghiệm có nội dung liên quan: giáo dục
- Chưa tìm thấy bằng chứng trong hồ sơ cho: React Native
- Mô hình chưa được kiểm chứng bằng đánh giá ghép mentor thực tế
- Đang phụ trách 0 nhóm

## Phạm vi và giới hạn

Đây là mô hình xếp hạng tuyến tính đã train, dùng xử lý văn bản theo quy tắc và danh mục chủ đề hữu hạn. Chưa hiểu đầy đủ mọi cách diễn đạt, ngữ cảnh nhiều câu, mức độ ưu tiên hoặc điều kiện bắt buộc. Có thể bỏ sót mô tả ít từ chuyên môn hoặc ngoài danh mục.
Ứng viên khớp một phần vẫn có thể xuất hiện; phần thiếu bằng chứng được ghi trong reasons. Ví dụ có kinh nghiệm mobile chưa chứng minh biết React Native. Không dùng điểm cao thay việc xác nhận năng lực. Chưa có nhãn ghép thật hoặc so sánh BGE-M3.

## Bằng chứng chạy

- CV thành công: 14/14 và 14/14 ở hai bộ.
- Marker riêng tư giả bị loại: True / True; chỉ là kiểm tra trên các mẫu này.
- Embedding/Ollama calls: 0 / 0.
- Demo dùng EF Core InMemory và storage giả; không qua HTTP/UI hoặc database thật. Các test API được ghi riêng trong báo cáo kiểm thử backend.
- Model SHA256: `c069e746e24ad9c64472deaca8b59088872242c76de0bd449585098f73368311`; giữ nguyên trong hai lần đánh giá.
- JSON lưu hash đầu vào, mã nguồn liên quan và assembly đã nạp. report.py từ chối kết quả cũ khi nguồn/đầu vào/model thay đổi. baseline/ giữ dữ liệu và kết quả v1.
- Bộ cũ: [projects.json](projects.json), [results.json](results.json). Bộ mới: [holdout-projects.json](holdout-projects.json), [holdout-results.json](holdout-results.json). CV: [mentors.json](mentors.json).

## Chạy lại từ gốc repo

```powershell
dotnet restore tools/mentor-matching/demo/Demo.csproj --disable-parallel -m:1
dotnet build tools/mentor-matching/demo/Demo.csproj --no-restore -m:1 -p:UseSharedCompilation=false
dotnet run --project tools/mentor-matching/demo/Demo.csproj --no-build --no-restore
dotnet run --project tools/mentor-matching/demo/Demo.csproj --no-build --no-restore -- . holdout-projects.json
python tools/mentor-matching/demo/report.py
```
