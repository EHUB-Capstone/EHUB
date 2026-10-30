# Phân công mentor bằng AI và xác nhận của admin

## Cách dùng

1. Mở **Subject Management → Lecturers & Mentors**, chọn kỳ có lớp và mentor đang hoạt động.
2. Chọn **AI → AI preview · all classes** để đề xuất cho cả kỳ. Để chỉ chia một lớp, chọn **Manage by class → AI preview** ở dòng lớp đó.
3. Kiểm tra mentor doanh nghiệp và mentor học thuật của từng nhóm, điểm phù hợp, lý do, mentor được kế thừa và các vị trí còn thiếu. Các đề xuất AI có điểm 0 được gắn **Needs review** và xuất hiện trong **Needs attention**, kể cả khi vị trí đã có mentor được đề xuất. Admin có thể sửa từng vị trí trong bản xem trước.
4. Bấm **Accept and assign mentors** để lưu. Đóng bản xem trước sẽ không tạo phân công. Thay mentor đang có phải được chọn rõ ràng và có lý do.

## Dữ liệu được xét

- Nhu cầu của nhóm: tên và mô tả dự án, hoặc tên và mô tả nhóm khi chưa có dự án. Nếu chưa có mô tả, AI vẫn đối chiếu tên hiện có và tạo đề xuất khi có mentor hợp lệ; thiếu mô tả không tự loại nhóm khỏi phân công.
- Hồ sơ mentor: chuyên môn, công nghệ, lĩnh vực startup, kinh nghiệm, giới thiệu và thành tích đã ghi trong hồ sơ.
- Lịch sử hướng dẫn: tiêu đề và ghi chú của các buổi **Completed** đã kết thúc, nằm trong thời gian mentor thực sự phụ trách nhóm. Lịch sử này tự trở thành dữ liệu đối chiếu ở lần đề xuất tiếp theo khi buổi hướng dẫn được hoàn tất và lưu ghi chú.
- Thành tích dự án: dự án phải được liên kết trực tiếp với phân công mentor; sự kiện thêm thành tích phải nằm trong thời gian mentor phụ trách. Sửa ghi chú, xóa nhãn hoặc sao chép từ kỳ trước không tự ghi công cho mentor mới. Dự án cũ chưa có lịch sử thay đổi dùng snapshot có thời điểm hợp lệ làm dữ liệu dự phòng.
- Tải hướng dẫn: số nhóm đang phụ trách trong cả kỳ được dùng để ưu tiên mentor ít nhóm hơn khi điểm phù hợp bằng nhau.

AI sử dụng provider đang được cấu hình của hệ thống (Trained, Hybrid hoặc Ollama). Điểm là điểm xếp hạng tương đối, không phải xác suất thành công. Số buổi hướng dẫn hoặc số thành tích đơn thuần không tạo ra chuyên môn phù hợp nếu không có nội dung liên quan.

Sau khi model tìm được bằng chứng phù hợp với dự án, luồng phân công AI cộng điểm ưu tiên từ lịch sử đã ghi nhận:

- Lịch sử hướng dẫn: 1 điểm mỗi nhóm đã có buổi hoàn tất (tối đa 5), cộng 1 điểm mỗi 2 buổi hoàn tất (tối đa 5), tổng tối đa 10 điểm.
- Thành tích nhóm: 2 điểm mỗi dự án có thành tích được ghi nhận đúng thời gian phụ trách (tối đa 5 dự án), tổng tối đa 10 điểm. Nhiều nhãn hoặc nhiều phân công trên cùng một dự án không được tính thành nhiều dự án.
- Điểm cuối = điểm phù hợp gốc + hai khoản ưu tiên, giới hạn 100. Mentor mới không bị trừ điểm. Nếu không có bằng chứng phù hợp, điểm và hai khoản cộng đều bằng 0; vẫn hiện số liệu lịch sử để admin xem.

Đây là quy tắc ưu tiên có giới hạn của thuật toán, không phải trọng số đã huấn luyện hoặc xác suất thành công. Nội dung buổi hướng dẫn/thành tích vẫn được dùng cho đối chiếu như trước; hai khoản điểm trên chỉ áp dụng cho phân công AI, không thay đổi điểm ở luồng gợi ý mentor thông thường.

Riêng luồng phân công dùng `MentorRankingScope.AllSpecialties`: chuyên môn được xét theo nội dung hồ sơ và lịch sử, không suy ra hoặc giới hạn chuyên môn từ nhãn Business/IT của loại mentor. Ví dụ, mentor doanh nghiệp có kinh nghiệm AI vẫn được đối chiếu với dự án AI. Quy tắc đúng vị trí Enterprise/Academic vẫn áp dụng; hệ thống không đổi loại mentor đã lưu. Luồng đề xuất mentor thông thường giữ nguyên phạm vi xếp hạng và bộ lọc hiện có. Các nội dung phủ nhận kinh nghiệm hoặc nhu cầu dự án đã loại trừ vẫn được tôn trọng.

## Kế thừa và quy tắc phân công

Thứ tự xử lý: chỉnh tay của admin → kế thừa mentor hợp lệ của nhóm EXE101 tiếp tục sang EXE201 qua `PreviousTeamId` → AI lấp các vị trí còn thiếu. Mentor cũ phải còn hoạt động và tham gia kỳ mới. Mentor đã có trong vị trí được giữ nguyên trừ khi admin yêu cầu thay.

AI chỉ chọn mentor có tài khoản, hồ sơ hoạt động, tham gia kỳ đã chọn và đúng loại mentor. Mentor chưa có tài khoản vẫn có thể được chọn thủ công hoặc qua Balanced/Random. Khi model trả kết quả hợp lệ nhưng không có ứng viên với bằng chứng chuyên môn phù hợp, AI vẫn lấp vị trí bằng mentor hợp lệ có tải thấp nhất trong cả kỳ. Đề xuất này giữ `fitScore: 0`, thêm lý do chưa có bằng chứng và yêu cầu admin rà soát; không tạo kỹ năng, kinh nghiệm hoặc độ tin cậy giả. Các đề xuất có bằng chứng được ưu tiên trước bước cân bằng tải này, kể cả khi điểm bằng 0.

Không có ứng viên hợp lệ đúng loại thì vị trí vẫn trống. AI không lấy mentor khác loại hoặc hồ sơ chưa có tài khoản để bù vào. Kết quả model thiếu hoặc không hợp lệ không được coi là kết quả “không có bằng chứng” để tự chia theo tải. Lỗi model/provider tiếp tục trả `503 / MENTOR_MATCHING_UNAVAILABLE` và không tạo bản xem trước ngẫu nhiên.

Bản xem trước dùng cùng cơ chế session của phân công hiện tại. AI tự tạo đầy đủ đề xuất; admin xem rồi bấm **Accept and assign mentors** một lần để chốt. Chỉ admin được tạo và xác nhận; session thuộc admin tạo nó, có thời hạn và chỉ dùng một lần. Khi dữ liệu phân công hoặc tải mentor thay đổi gây xung đột, cần tạo lại bản xem trước. Phân công và Outbox được lưu cùng transaction hiện có. Audit log ghi admin chốt, thời điểm UTC và số dòng AI; không thêm bước xác minh hoặc checkbox bắt buộc.

## Lịch sử hiển thị và giới hạn

Hồ sơ cá nhân và danh bạ trả thêm `history`: `completedSessionCount`, `mentoredTeamCount`, `achievementCount`. Dòng phân công AI trả `history` gồm các số đếm này cùng `baseFitScore`, `historyBonus`, `achievementBonus`; giao diện hiển thị để admin xem cùng điểm cuối trước khi chốt. Nội dung ghi chú nhóm trước chỉ dùng nội bộ để đối chiếu; không được chép vào hồ sơ công khai hoặc trả nguyên văn trong lý do phân công. Semantic reranker chỉ nhận hồ sơ chuyên môn công khai vì provider này có thể trích dẫn đầu vào.

Tổng số buổi hướng dẫn được tính trên toàn bộ lịch sử hợp lệ. Nội dung đối chiếu được giới hạn ở các buổi và thành tích gần đây để giữ chi phí xử lý có giới hạn. Buổi Scheduled, Cancelled, NoShow, trong tương lai hoặc nằm ngoài khoảng phân công không được tính.

Không thay đổi schema database, không cần migration. API cũ Balanced/Random và chỉnh tay tiếp tục dùng cùng request; chiến lược mới là `strategy: "Ai"`. Dòng đề xuất AI có thêm `fitScore` và `reasons`. Nếu model không sẵn sàng, API trả `503 / MENTOR_MATCHING_UNAVAILABLE`, không tạo phân công hoặc đề xuất ngẫu nhiên dưới tên AI.

## Kiểm thử liên quan

- `MentorAiAllocationTests`: chọn theo từng nhóm và loại mentor, phạm vi lớp/kỳ, ưu tiên bằng chứng trước tải, lấp các vị trí thiếu bằng chứng với điểm 0, cân bằng tải và tính lặp lại theo seed, nhóm chỉ có tên, bảo toàn vị trí hiện có, chỉnh tay, kế thừa, loại hồ sơ chưa có tài khoản, thiếu ứng viên đúng loại và lỗi/kết quả model không hợp lệ.
- `MentorAllocationSpecialtyTests`: phân công xét mọi chuyên môn có bằng chứng trong hồ sơ theo `AllSpecialties`, giữ nguyên loại mentor và hành vi đề xuất thông thường, tôn trọng nội dung phủ nhận/loại trừ, dùng lịch sử nội bộ cho model nhưng chỉ đưa bằng chứng công khai cho semantic reranker và không tự chia khi model không sẵn sàng.
- `MentorHistoryEvidenceTests`: khoảng thời gian, trạng thái buổi hướng dẫn, thành tích liên kết, giới hạn nội dung, đối chiếu kỹ năng từ lịch sử và giữ nguyên hồ sơ đã lưu.
- `MentorHistoryRankingTests`: ưu tiên mentor có lịch sử/thành tích khi có bằng chứng phù hợp, không tạo độ phù hợp từ số đếm, giới hạn 20 điểm cộng/100 điểm cuối và không trừ điểm mentor mới.
- `MentorRecommendationRankerTests`: provider đúng cấu hình, lỗi model, kết quả không hợp lệ và bằng chứng phù hợp.
- `MentorAiAllocationIntegrationTests`: PostgreSQL, đề xuất từ lịch sử, chỉ ghi khi chấp nhận, Outbox, chống xác nhận lại, quyền 401/403 và lỗi 503.
- `frontend/tests/mentorAllocationPreview.test.ts`: truyền phạm vi lớp/chiến lược AI, giữ điểm 0 và lý do trên đúng vị trí, đưa đề xuất AI mới có điểm 0 vào danh sách cần rà soát dù đã lấp vị trí, không gắn trạng thái này cho chỉnh tay/kế thừa/chiến lược khác, kết hợp bộ lọc lớp, hủy request và chỉ gọi commit khi chấp nhận.

## Kết quả kiểm tra ngày 30/10/2026

- `dotnet build EHub.slnx --no-restore`: thành công, 0 lỗi; còn cảnh báo dependency/test sẵn có.
- `dotnet test EHub.slnx --no-build --no-restore --filter "FullyQualifiedName!~IntegrationTests"`: 141 UnitTests và 725 ApplicationTests đạt.
- `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build --no-restore`: 517 đạt, 2 test model cục bộ bỏ qua, không thất bại. Luồng AI gửi đúng một request chốt với `sessionId`, giữ Outbox/assignment cùng transaction và ghi admin/thời điểm chốt vào audit.
- Frontend: `npm run lint`, `npm run type-check`, `npm run build` đạt; `npm test` có 507 test đạt.
- `git diff --check`: sạch.

Thử UI riêng tại `http://127.0.0.1:5175` với API thật ở 5227, PostgreSQL mới và toàn bộ danh sách mentor/dự án giả lập. AI tạo 200 đề xuất cho 100 nhóm, không còn vị trí Enterprise/Academic trống. Ví dụ M001 có 4 buổi hoàn tất, 2 nhóm và 2 dự án có thành tích: điểm gốc 81, lịch sử +4, thành tích +4, điểm cuối 89. Nút **Accept and assign mentors** dùng được ngay, không có checkbox xác minh; bản đề xuất được để mở, chưa chốt. Các đề xuất không có bằng chứng vẫn có điểm 0/cảnh báo theo quy tắc đã nêu, nên số vị trí được lấp không phải độ chính xác ghép mentor. Bản demo import cũ ở 5174/5226 được giữ nguyên. Bằng chứng ở `artifacts/mentor-ui-demo/history-ranking/` được Git ignore.

## Kết quả kiểm tra ngày 29/10/2026

Chạy từ `backend/`:

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build EHub.slnx --no-restore` | Thành công, 0 lỗi; còn cảnh báo dependency sẵn có. |
| `dotnet test EHub.slnx --no-build --no-restore --filter "FullyQualifiedName!~IntegrationTests"` | 141 UnitTests và 672 ApplicationTests đạt. |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-build --no-restore` | 480 đạt, 2 test model cục bộ bỏ qua, không thất bại. |

Chạy từ `frontend/`:

| Lệnh | Kết quả |
| --- | --- |
| `node --experimental-strip-types --test tests/mentorAllocationPreview.test.ts tests/darkThemeCoverage.test.ts tests/noArbitraryHex.test.ts tests/designTokens.test.ts` | 24 đạt, không có test thất bại hoặc bỏ qua. |
| `npm run lint` | Exit 0; còn các cảnh báo lint sẵn có ở những phần khác. |
| `npm run type-check` | Đạt. |
| `npm test` | 481 đạt, không có test thất bại hoặc bỏ qua. |
| `npm run build` | Thành công. |

`git diff --check` sạch. Hai ca tích hợp mới kiểm tra việc thiếu bằng chứng vẫn đề xuất đúng loại và chỉ lưu sau xác nhận, cùng việc kinh nghiệm kỹ thuật của mentor Enterprise được xét mà không đổi loại hoặc lộ lịch sử riêng.

Kiểm tra UI dùng API thật, PostgreSQL riêng và model thử nghiệm với 30 hồ sơ doanh nghiệp đã ẩn danh: 100 dự án trong 10 lớp nhận 100 đề xuất Enterprise, gồm 88 đề xuất có bằng chứng và 12 đề xuất theo tải có điểm 0/cảnh báo rà soát. Đây là độ phủ phân công, không phải đánh giá độ chính xác phù hợp. Không có mentor Academic trong dữ liệu này nên 100 vị trí Academic còn thiếu. Sau khi chấp nhận trên UI và tải lại trang, có 100 vị trí Enterprise ở trạng thái Current, 0 vị trí Enterprise thiếu và nút chấp nhận trùng bị khóa. Bộ lọc lớp trả 10 nhóm; tìm tên dự án trả đúng một nhóm. Bằng chứng và ảnh nằm trong `artifacts/mentor-ui-demo/` đã được Git ignore.

Lượt demo tiếp theo thêm 10 hồ sơ IT giả lập (`tools/mentor-matching/ui-demo/it-mentors.json`) dưới loại Academic/IT, giữ 30 hồ sơ Enterprise và bắt đầu database demo mới với 0 phân công. Một lần bấm **AI preview · all classes** hiển thị 200 đề xuất trên 100 nhóm: 100 Enterprise và 100 Academic, cả hai số vị trí thiếu đều bằng 0. Có 12 đề xuất Enterprise và 82 Academic điểm 0 cần rà soát; 10 dự án của lớp kỹ thuật đều có đề xuất IT với điểm dương và lý do từ kinh nghiệm. Điểm không phải độ chính xác đã được kiểm chứng. API thật xác minh 401, đăng nhập thường, đúng loại mentor, 200 cặp nhóm/vị trí duy nhất và không lưu phân công khi chỉ xem trước. Bản xem trước được để mở cho admin kiểm tra và chấp nhận. Bằng chứng riêng của lượt này nằm trong `artifacts/mentor-ui-demo/it40/`; build công cụ demo thành công với 0 lỗi/cảnh báo.

## Kết quả kiểm tra ngày 27/10/2026

Chạy từ `backend/`:

| Lệnh | Kết quả |
| --- | --- |
| `dotnet build EHub.slnx --no-restore` | Thành công, không có lỗi build. |
| `dotnet test EHub.slnx --no-build --no-restore --filter "FullyQualifiedName!~IntegrationTests"` | 141 UnitTests và 652 ApplicationTests đạt. |
| `dotnet test tests/EHub.IntegrationTests/EHub.IntegrationTests.csproj --no-restore` | 478 đạt, 2 bỏ qua; không có test thất bại. |

Hai test bỏ qua là các test cần bật chạy model ONNX/BGE-M3 cục bộ. Luồng phân công AI mới được kiểm tra với `FileMentorFitRanker` thật và PostgreSQL trong Testcontainers, gồm lịch sử buổi hướng dẫn và sự kiện thành tích.

Chạy từ `frontend/`:

| Lệnh | Kết quả |
| --- | --- |
| `npm run lint` | Exit 0; còn các cảnh báo lint sẵn có ở những phần khác. |
| `npm run type-check` | Đạt. |
| `npm test` | 479 đạt, không có test thất bại hoặc bỏ qua. |
| `npm run build` | Thành công. |

`git diff --check` sạch. Backend còn cảnh báo dependency sẵn có cho `System.Security.Cryptography.Pkcs` 10.0.0 và một số cảnh báo test cũ; thay đổi này không nâng cấp dependency.
