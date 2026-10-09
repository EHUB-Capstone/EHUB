# E-HUB Frontend Coding Standard & UI Requirements

Tài liệu này định nghĩa quy chuẩn code và yêu cầu giao diện cho lập trình viên phát triển Frontend E-HUB nhằm đảm bảo mã nguồn nhất quán, dễ bảo trì, dễ mở rộng và mang lại trải nghiệm người dùng ổn định trên các vai trò Admin, Lecturer, Mentor và Student.

---

## 1. Nguyên tắc Kiến trúc Frontend

1.  **Tách rõ trách nhiệm theo thư mục:**
    *   `src/pages`: Chứa các màn hình chính, chịu trách nhiệm điều phối layout, gọi hooks và truyền dữ liệu xuống component con.
    *   `src/components`: Chứa component tái sử dụng, không gắn chặt với routing nếu không cần thiết.
    *   `src/features`: Chứa module chức năng có logic riêng, ví dụ Execution Board hoặc Data Bank.
    *   `src/api`: Chứa toàn bộ hàm gọi API, không gọi `axios` trực tiếp rải rác trong component.
    *   `src/hooks`: Chứa custom hooks dùng lại giữa nhiều màn hình.
    *   `src/context`: Chứa state dùng toàn ứng dụng như authentication hoặc theme.
    *   `src/utils`: Chứa hàm tiện ích thuần, không phụ thuộc vào UI.
2.  **Không trộn business flow phức tạp vào UI component:**
    *   Component chỉ nên xử lý hiển thị, tương tác người dùng và gọi callback.
    *   Logic lấy dữ liệu, biến đổi dữ liệu, mutation hoặc đồng bộ server state nên đưa vào API layer, hooks hoặc feature-specific utilities.
3.  **Ưu tiên component dùng lại:**
    *   Các UI phổ biến như Button, Card, Modal, Input, Badge, EmptyState, ErrorState phải dùng component có sẵn trong `src/components/ui`.
    *   Không tự tạo lại button/input/modal riêng nếu component hiện có đáp ứng được nhu cầu.

---

## 2. Quy tắc TypeScript & React

1.  **Bắt buộc dùng TypeScript rõ ràng:**
    *   Không dùng `any` nếu không có lý do thật sự cần thiết.
    *   Ưu tiên định nghĩa `type` hoặc `interface` cho props, API response, request payload và dữ liệu hiển thị.
    *   Với dữ liệu có thể rỗng, phải thể hiện rõ bằng `null`, `undefined` hoặc union type.
2.  **Quy tắc component:**
    *   Component dùng PascalCase, ví dụ `StudentDashboard`, `TeamWorkspace`, `RubricScoringForm`.
    *   File component dùng PascalCase và trùng tên component chính.
    *   Component nên là function component.
    *   Props interface/type đặt tên theo dạng `<ComponentName>Props`.
3.  **Quy tắc hooks:**
    *   Custom hook bắt đầu bằng `use`, ví dụ `useAuth`, `useExecutionBoard`, `usePresence`.
    *   Hook không được gọi có điều kiện.
    *   Hook nên trả về dữ liệu, trạng thái loading/error và action cần thiết thay vì trả quá nhiều implementation detail.
4.  **Quy tắc state:**
    *   State cục bộ của UI dùng `useState`.
    *   Server state dùng TanStack React Query.
    *   State toàn ứng dụng chỉ đưa vào Context khi nhiều vùng độc lập cùng cần dùng.
    *   Không lưu dữ liệu có thể tính toán được vào state nếu có thể derive từ props hoặc query result.

---

## 3. Quy tắc gọi API

1.  **Tập trung API trong `src/api`:**
    *   Mỗi nhóm nghiệp vụ nên có file API riêng, ví dụ `authApi.ts`, `classApi.ts`, `workspaceApi.ts`.
    *   Component không gọi trực tiếp `fetch` hoặc `axios` nếu chưa thông qua API layer.
2.  **Chuẩn hóa request/response:**
    *   Kiểu dữ liệu request và response phải được định nghĩa rõ.
    *   Nếu Backend trả về cấu trúc `ApiResponse`, Frontend phải unwrap dữ liệu nhất quán tại API layer hoặc query hook.
3.  **Xử lý lỗi:**
    *   Lỗi API phải được hiển thị bằng thông báo rõ ràng, ưu tiên `react-hot-toast` hoặc component `ErrorState`.
    *   Không hiển thị raw exception, stack trace hoặc thông tin kỹ thuật nhạy cảm cho người dùng cuối.
4.  **Loading và empty state:**
    *   Mọi màn hình gọi API phải có trạng thái loading.
    *   Danh sách rỗng phải có empty state, không để vùng trắng gây hiểu nhầm là lỗi giao diện.

---

## 4. Routing & Phân quyền

1.  **Quản lý route bằng React Router:**
    *   Route public gồm các trang như Home, Login, Register, ForgotPassword.
    *   Route yêu cầu đăng nhập phải đi qua ProtectedRoute hoặc cơ chế bảo vệ tương đương.
2.  **Phân quyền theo vai trò:**
    *   Các màn hình Admin, Lecturer, Mentor, Student phải kiểm tra quyền truy cập rõ ràng.
    *   Khi người dùng không có quyền, chuyển tới trang Forbidden hoặc hiển thị thông báo phù hợp.
3.  **Không dựa hoàn toàn vào frontend để bảo mật:**
    *   Frontend chỉ kiểm soát trải nghiệm điều hướng.
    *   Backend vẫn phải là nơi xác thực và phân quyền cuối cùng.

---

## 5. Quy tắc Form & Validation

1.  **Form phải có validation phía client:**
    *   Trường bắt buộc phải được kiểm tra trước khi submit.
    *   Email, số điện thoại, URL, mật khẩu hoặc file upload phải có validation phù hợp.
2.  **Thông báo lỗi phải gần vị trí nhập liệu:**
    *   Không chỉ toast lỗi chung nếu người dùng cần sửa một field cụ thể.
    *   Field lỗi phải có trạng thái hiển thị rõ ràng.
3.  **Trạng thái submit:**
    *   Nút submit phải có loading state khi request đang chạy.
    *   Chặn double submit bằng cách disable nút trong lúc xử lý.
4.  **Không làm mất dữ liệu người dùng nhập:**
    *   Khi API lỗi, giữ lại dữ liệu form để người dùng sửa.
    *   Chỉ reset form sau khi thao tác thành công.

---

## 6. Quy chuẩn Giao diện (UI Requirements)

1.  **Nhất quán hệ màu E-HUB:**
    *   Màu chính: `primary` cam `#F37021`.
    *   Màu phụ: `secondary` xanh `#034EA2`.
    *   Màu thành công: `success` xanh lá `#51B848`.
    *   Màu cảnh báo và lỗi dùng token đã khai báo trong `src/index.css`.
    *   Không hardcode màu mới nếu màu đó đã có trong theme.
2.  **Typography:**
    *   Font mặc định là Inter.
    *   Heading, label, body text phải có phân cấp rõ ràng.
    *   Không dùng quá nhiều kích thước chữ khác nhau trong cùng một màn hình.
3.  **Layout:**
    *   Dashboard và màn hình quản trị ưu tiên layout rõ ràng, dễ quét thông tin, không trang trí quá mức.
    *   Khoảng cách giữa các vùng nội dung phải nhất quán.
    *   Không đặt card lồng card nếu không có lý do giao diện rõ ràng.
4.  **Component UI:**
    *   Button phải dùng component `Button` trong `src/components/ui/Button.tsx` khi phù hợp.
    *   Modal phải có tiêu đề, hành động chính, hành động hủy và trạng thái đóng rõ ràng.
    *   Table hoặc list phải có loading, empty và error state.
    *   Icon nên dùng `lucide-react`.
5.  **Responsive:**
    *   Mọi màn hình phải sử dụng được trên desktop, tablet và mobile.
    *   Sidebar, table, form dài và board kéo thả phải có phương án hiển thị phù hợp trên màn hình nhỏ.
    *   Text không được tràn khỏi button, card, modal hoặc table cell.
6.  **Accessibility cơ bản:**
    *   Button, input, select, textarea phải có label hoặc aria-label phù hợp.
    *   Màu chữ và nền phải đủ tương phản.
    *   Không dùng màu sắc làm tín hiệu duy nhất cho trạng thái lỗi/thành công.
    *   Modal phải có khả năng đóng bằng nút rõ ràng.
7.  **Trạng thái tương tác:**
    *   Button, link, input và item có thể click phải có hover/focus/disabled state.
    *   Thao tác nguy hiểm như xóa, reset, remove thành viên phải có confirm dialog.
    *   Thao tác thành công hoặc thất bại phải có feedback rõ ràng.

---

## 7. Hiệu năng & Trải nghiệm người dùng

1.  **Tối ưu render:**
    *   Không tạo object/function lớn lặp lại trong render nếu gây re-render không cần thiết.
    *   Dùng memoization khi component con nặng hoặc danh sách lớn có dấu hiệu chậm.
2.  **Danh sách lớn:**
    *   Table/list nhiều dữ liệu nên có phân trang, filter hoặc search.
    *   Không render toàn bộ dữ liệu lớn nếu có thể phân trang từ API.
3.  **Realtime:**
    *   Socket.IO chỉ nên kết nối trong màn hình hoặc context thật sự cần realtime.
    *   Phải cleanup listener khi component unmount.
4.  **Animation:**
    *   Animation nên hỗ trợ mục đích giao diện, ví dụ chuyển trạng thái, mở modal, cập nhật board.
    *   Không lạm dụng animation làm chậm thao tác chính.

---

## 8. Bảo mật Frontend

1.  **Không hardcode secrets:**
    *   Không commit API key, secret key, token hoặc credential vào source code.
    *   Biến môi trường frontend phải dùng prefix phù hợp với Vite, ví dụ `VITE_API_BASE_URL`.
2.  **Token và dữ liệu nhạy cảm:**
    *   Không log token, refresh token, password hoặc thông tin cá nhân nhạy cảm ra console.
    *   Không hiển thị dữ liệu người dùng không thuộc quyền truy cập hiện tại.
3.  **Xử lý HTML động:**
    *   Tránh dùng `dangerouslySetInnerHTML`.
    *   Nếu bắt buộc render HTML từ server, phải sanitize trước khi hiển thị.

---

## 9. Quy tắc Lint, Build & Review

1.  **Trước khi tạo Pull Request:**
    ```bash
    npm run type-check
    npm run lint
    npm run build
    ```
2.  **Checklist review frontend:**
    *   Không có TypeScript error.
    *   Không có lint error.
    *   Màn hình có loading, empty và error state nếu gọi API.
    *   UI responsive ở mobile và desktop.
    *   Không hardcode dữ liệu test nếu màn hình đã có API.
    *   Không gọi API trực tiếp trong component khi đã có API layer phù hợp.
    *   Không làm lộ token, secret hoặc thông tin nhạy cảm trong log.

---

## 10. Quy ước đặt tên

*   **Component/Page:** PascalCase, ví dụ `ClassManagement`, `MentorDashboard`.
*   **Hook:** camelCase và bắt đầu bằng `use`, ví dụ `useAuth`.
*   **Function/Variable:** camelCase, ví dụ `fetchClasses`, `selectedTeamId`.
*   **Type/Interface:** PascalCase, ví dụ `ClassResponse`, `CreateTeamRequest`.
*   **Constant:** UPPER_SNAKE_CASE hoặc PascalCase tùy ngữ cảnh, ví dụ `DEFAULT_PAGE_SIZE`.
*   **File API:** camelCase theo module, ví dụ `authApi.ts`, `teamWorkspaceApi.ts`.
*   **CSS utility helper:** Đặt trong `utils` nếu dùng lại nhiều nơi, ví dụ `cn.ts`.
