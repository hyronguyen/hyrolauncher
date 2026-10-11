# HYTCG

HYTCG là plugin tích hợp của HYRO Launcher, sử dụng WinForms/WebView2 và native bridge hiện có. Không cần Electron/Tauri, backend server, database hay cloud storage.

## Sử dụng

- Mở **HYTCG** từ thanh plugin.
- **Open x1 / Open x10**: random nhân vật và rarity, chọn artwork, dựng card, tự lưu ảnh trong container mã hóa `.hycard`.
- Mỗi pack x10 xử lý tối đa **2 card** đồng thời mặc định, bao gồm fetch, render và save. Có loading/lỗi/retry riêng từng card.
- **Bộ sưu tập** lọc và phân trang metadata đã ký trước khi đọc ảnh; chỉ xác thực ciphertext/đọc preview của trang đang xem. Hỗ trợ tìm tên/series, lọc rarity và yêu thích, sắp xếp theo ngày/tên/độ hiếm. Preview lỗi hoặc thẻ bị sửa được bỏ qua; `NextOffset` giữ phân trang chính xác khi bỏ thẻ không hợp lệ.
- Nút mở rộng ở header HYTCG chuyển panel giữa kích thước gọn và tối đa **1120 × 880**, luôn nằm trong vùng màn hình làm việc. Thu nhỏ trả về kích thước cũ. Logo SVG lấy màu accent của launcher.
- Nếu save lỗi, card vẫn hiển thị và có nút **Lưu** để thử lại. Không mở pack mới khi còn card chưa lưu.
- Artwork biến thể chỉ hiện **ALT**; nguồn dữ liệu chỉ hiện ở hai nút liên kết **ACDB / ZeroChan**. PNG không in tên nguồn.
- Khung tự chọn theo rarity: **Common / Uncommon / Rare / SR** bỏ header; artwork ban đầu fit đầy đủ để giữ phần đầu nhân vật, thông tin nằm ở footer nền trung tính. **SSR / UR** giữ artwork tràn viền. Viền dày hơn, có mặt vát sáng/tối và họa tiết riêng: Common giản lược, Uncommon đường đôi, Rare hình thoi, SR sao và nét khắc, SSR cung kim loại, UR sao và prism. Dye chỉ đổi viền và họa tiết khung, không đổi màu artwork/nền thông tin/chữ và không thêm nền riêng cho chữ. Thẻ cũ được cập nhật cách hiển thị mà không sửa file `.hycard` đang sở hữu; nếu ảnh đã bị crop trước đây, native lấy lại đúng artwork gốc khi còn truy cập được và lưu cache bảo vệ local.
- Sáu rarity có **emblem SVG riêng**, dùng màu cố định độc lập với accent và dye: Common bạc, Uncommon xanh lá, Rare xanh lam, SR tím, SSR vàng, UR hồng. Emblem được ghép vào ảnh thẻ và dùng trên giao diện; thay dye không đổi màu nhãn rarity.
- Mở chi tiết để xem card lớn, nghiêng 3D theo chuột; holo dùng lớp phủ nhẹ: **SR vân satin, SSR lưới ánh kim, UR hạt sao/prism**. Nút `3D` bật/tắt hiệu ứng và ghi nhớ lựa chọn trên máy. **Mystic** có quầng sáng chuyển động liên tục như ánh lửa, bám theo mặt thẻ khi nghiêng thay vì nằm sau nền panel. Hiệu ứng tuân theo công tắc của HYTCG; Windows giảm chuyển động chỉ giảm tốc độ vẽ, không tự tắt Mystic. Vùng nhận chuột đứng yên, chỉ mặt card xoay để tránh giật ở mép.
- Quy tắc trong plugin chỉ để xem. Tỷ lệ và giá trị phân giải được cố định trong native code.

## Dev mode

Nhấn **5 lần liên tiếp vào icon HYTCG** trong vòng 3 giây để bật phiên thử nghiệm **10 phút**. Có thể dùng icon trên thanh plugin launcher hoặc logo trong panel. Nhãn **DEV** hiện thời gian còn lại; nhấn dấu **×** để thoát sớm. Đóng/chuyển plugin, hết giờ hoặc đóng launcher đều kết thúc phiên.

Phiên dev dùng bản sao riêng trong thư mục tạm, mở tiền/lượt chơi/vật phẩm và cấp độ để thử chức năng. Ví, card, artwork, album và tiến trình thật không nhận thay đổi từ phiên này. Thao tác còn chạy bị hủy khi thoát; dữ liệu tạm được xóa rồi quay về tài khoản trước khi vào dev. Launcher cũng dọn phiên tạm của tiến trình đã kết thúc nếu lần trước bị ngắt đột ngột. Dev mode không tự bật lại khi khởi động.

## Lượt mở và gems

- **1 lượt = 1 card**: Open x1 dùng 1 lượt; Open x10 dùng 10 lượt.
- Khởi tạo lần đầu có 5 lượt miễn phí. Hồi **1 lượt mỗi 20 phút**, tích tối đa **5**; thời gian đóng launcher cũng được tính. Khi đủ 5, không tích thời gian dư.
- Chọn **Lượt mở** hoặc **Gems** trên Booster. Thanh toán trực tiếp **160 gems/card**, x10 là **1.600 gems**, không cần đổi gems sang credit trước. Những lượt mua từ phiên bản trước vẫn được giữ riêng và dùng trong chế độ Lượt mở.
- Bảo hiểm **SR sau tối đa 50 lượt**, **SSR sau tối đa 100 lượt**, **UR sau tối đa 500 lượt** không nhận được cấp tương ứng hoặc cao hơn. Nhận thẻ cùng cấp/cao hơn reset bộ đếm tương ứng; nếu nhiều mốc cùng đến, ưu tiên UR → SSR → SR. Chỉ card được native cấp thành công mới tính lượt: fetch lỗi/refund không tăng bộ đếm, recovery/fallback không tăng lần nữa. Nâng cấp thẻ không tăng hoặc reset pity.
- Phân giải card đã lưu có xác nhận tên card/gems; thẻ bị loại khỏi Collection và cộng gems một lần. Giá trị = base của rarity + `floor(AniList favourites / 100)` khi xác định đúng nhân vật và series; không tìm thấy hoặc API không khả dụng thì dùng base. Native làm ấm cache trước khi xem số gems/phân giải nhiều thẻ, gom tối đa 8 nhân vật trong một truy vấn. Renderer không được tự khai rarity, favourites hoặc số gems.
- Reserve/debit trước khi gọi API. Nếu hết số lần thử ID mà không tìm được nhân vật, lượt đó kết thúc và nhận **160 gems**, ô kết quả hiện `+160 gems`, không có nút retry. Biên nhận được lưu atomic cùng ví để replay/restart không nhận tiền lần nữa. Biên nhận 10 gems của phiên bản trước vẫn đọc được và không được trả lần nữa. Lỗi kết nối, rate limit, browser challenge hoặc hủy vẫn hoàn lại lượt; không phải lỗi tìm hết ID. Card đã cấp phải tiếp tục nhận đúng nhân vật/rarity cũ, không thể đóng panel để reroll miễn phí.
- Đóng plugin huỷ fetch; card đã cấp nhưng chưa lưu được giữ trong vault, hiện nút **Tiếp tục nhận card** khi mở lại.

## Yêu thích, phân giải và nâng cấp

- Nút trái tim lưu trạng thái yêu thích vào ví local được bảo vệ, giữ nguyên khi nâng cấp. Bộ lọc Yêu thích dùng dữ liệu native. Thẻ yêu thích vẫn có thể dùng làm nguyên liệu/phân giải nếu người dùng chủ động chọn; giao diện báo rõ khi lựa chọn có thẻ yêu thích.
- Phân giải nhiều thẻ hỗ trợ tối đa 100 thẻ/giao dịch, có kiểm tra toàn bộ chữ ký/hash và số gems native. Nếu một thẻ bị sửa, mất, đang khóa trong nâng cấp hoặc không hợp lệ thì không tiêu thụ thẻ nào trong giao dịch đó.
- Nâng cấp thẻ lên **một cấp liền kề** cần **10 thẻ của cấp đích** (nhân vật bất kỳ), hoặc **1 thẻ của cấp đích cùng character ID**. Thẻ chính không được nằm trong nguyên liệu; UR đã là cấp tối đa. Ví dụ Common → Uncommon cần 10 Uncommon, hoặc 1 Uncommon cùng nhân vật với thẻ chính.
- Có lựa chọn giữ artwork hoặc random artwork Zerochan mới. Khi giữ artwork, native tải lại đúng nguồn cũ; khi đổi artwork mà Zerochan không có ảnh/không khả dụng, dùng lại artwork cũ. Danh tính, cardId, yêu thích và ngày tạo của thẻ chính được giữ nguyên; rarity, thiết kế khung và revision được cập nhật. Dye và artwork riêng đã áp dụng vẫn gắn với thẻ chính; nâng rarity tự chuyển khung sang cấp mới.
- Frame dựng lại từ artwork nguồn, không cắt khung cũ khỏi ảnh PNG đã ghép. **Mọi thẻ mới đều dọc, 750 × 1050**, kể cả artwork nguồn nằm ngang. Thẻ ngang của phiên bản trước được native chiếu sang cách hiển thị dọc trong Collection, chi tiết, Studio và album; không xoay thẻ trong ô và không sửa ảnh gốc, model orientation hoặc chữ ký đang sở hữu.
- Nâng cấp chuẩn bị một intent local, khóa thẻ chính/nguyên liệu, tải artwork và render PNG mới. File mới có revision và nonce riêng, không ghi đè file đang sở hữu. Native flush ảnh mới xuống đĩa trước khi commit quyền sở hữu mới cùng tombstone nguyên liệu trong một giao dịch. Lỗi trước commit không mất thẻ/gems; có thể tiếp tục hoặc hủy nâng cấp đang chờ. Save retry không tiêu thụ nguyên liệu lần thứ hai.

## Atelier: album, Studio và nhân vật

Mở tab **Atelier** để dùng **Album**, **Studio**, **Thám hiểm / Explore**, **Túi đồ / Items**, **Đấu trường / Arena** và **Gắn kết / Bond**. Từ chi tiết thẻ có thể mở thẳng Studio hoặc Gắn kết. Chuyển vào Atelier hoặc nhấn Làm mới để lấy collection, vật phẩm và tiến trình mới nhất. Bộ chọn nhân vật hỗ trợ tìm tên/series và tải thêm; ảnh chỉ tải khi cần xem.

### Album và Studio

- **Album**: mua một cuốn **2.000 gems**, chọn bìa và nền trang, tặng **4 ô**. Mỗi ô thêm giá **200 gems**; tối đa **24 album × 120 ô**. Mở cuốn ở chế độ showcase, mỗi trang 6 ô, mỗi lượt lật một spread 12 ô. Nhấn **bút** để sửa tên, chọn thẻ, kéo đổi vị trí và lưu; đổi bìa/nền giá **200 gems**, không đổi thì không tính phí. Một thẻ xuất hiện tối đa một lần trong cùng album. Album cũ giữ thứ tự/thẻ và được chuyển sang capacity tối thiểu 4 ô mà không trừ gems. Xóa album chỉ xóa bố cục; thẻ vẫn thuộc Collection.
- **Studio**: chọn thẻ, xem khung theo rarity, chọn dye từ **Túi đồ**, rồi **Áp dụng**. Khung hiện tại chỉ để xem; thay đổi thiết kế bằng **Nâng rarity** trong chi tiết thẻ. Bản xem thử dùng cùng bộ ghép ảnh native như thẻ đã lưu. Dye/morph gộp thành **frame dye**; không có bộ chọn màu tự do. **Booster dye** nằm ở màn **Booster chính**, giá **50 gems/lượt**, native chọn đều một trong **64 màu** với **1%** khả năng nhận biến thể **Mystic**. Áp dụng một dye khác lên thẻ tiêu thụ **1 dye** đã mua; không thu thêm phí áp dụng. Dye thường chỉ đổi màu viền, Mystic thêm quầng sáng chuyển động gắn với thẻ; chữ, artwork và emblem rarity giữ màu ban đầu. Các menu Atelier nằm chung trên header dưới dạng icon; file/URL/sketch chỉ hiện sau khi chọn nguồn tương ứng.
- **Tạo, mua, nhập ảnh khung và gắn vật phẩm khung đã bỏ**. Native từ chối các lệnh này bằng `FrameRetired`; mọi card dùng khung tự động theo rarity. Bản ghi khung cũ vẫn được giữ trong vault để đọc và xác thực tài khoản cũ, nhưng không hiện trong Túi đồ và không thể dùng lại. Cập nhật không xóa card, gems, dye, artwork riêng hoặc tiến trình.
- Artwork riêng có ba cách: **Chọn ảnh** PNG/JPG trên máy, nhập liên kết HTTPS công khai rồi **Tải liên kết**, hoặc vẽ trên bảng trong suốt và chọn **Dùng bản vẽ**. Đăng ký ảnh/bản vẽ và xem thử chưa trừ gems. Native chuyển về palette **64 màu RGB**, giữ alpha, rồi đếm màu thực tế và pixel có alpha > 0: **ceil(pixel × số màu / 10.000) gems**. Ví dụ ảnh đặc **750 × 1050** dùng 1 màu có phí **79 gems**, dùng 8 màu có phí **630 gems**. Bản vẽ trống có phí 0; nét vẽ 2 pixel/1 màu có phí 1. Phí giống nhau cho sketch, file và URL. Ảnh gốc không thu phí. Artwork riêng đã áp dụng trước bản này giữ nguyên, không tự chuyển màu hoặc thu thêm phí.
- **Căn chỉnh artwork** mở các thanh thu phóng **100–400%** và vị trí ngang/dọc **−100–100%**. Có thể kéo trực tiếp phần artwork trên preview, dùng phím mũi tên để dịch chuyển, giữ Shift để dịch bước lớn hơn hoặc Home để căn giữa. Native lưu `ArtworkScale` trong **1–4** và `ArtworkX`/`ArtworkY` trong **−1–1**; mỗi đơn vị offset bằng nửa chiều vùng artwork của frame. Common–SR ban đầu fit toàn bộ ảnh; SSR–UR ban đầu phủ vùng artwork. Zoom và offset chỉ tác động đến artwork, mọi phần vượt vùng frame được cắt khi hiển thị. **Căn giữa và đặt lại** trả về `1/0/0`. Preview, báo giá và ảnh đã áp dụng dùng cùng phép ghép native; các giá trị cũng nằm trong khóa cache. **Chỉ đổi vị trí/zoom có phí 0 gems**, không nhập lại ảnh hoặc tiêu thụ lại dye đã áp dụng.
- Lưu cùng diện mạo hoặc gửi lại giao dịch không trừ gems/dye lần nữa. **Khôi phục** miễn phí trả về dye và artwork gốc; **Khôi phục trang trí trước** áp dụng lại dye/artwork vừa khôi phục, không mua lại artwork hay tiêu thụ dye lần nữa. Cả hai luôn dùng khung theo rarity hiện tại. Dye, gems, album, worker và tiến trình hiện có được giữ lại.
- Native nhận tối đa **8 MB**, chỉ PNG/JPEG có kích thước tối đa **4096 × 4096**. Ảnh được giải mã, bỏ metadata và chuẩn hóa PNG với cạnh dài tối đa **1600 px**, dung lượng tối đa **4 MB**. Artwork trùng được nhận diện bằng SHA-256; kho local có tối đa **64 ảnh**. Artwork đã đăng ký vẫn được giữ khi khôi phục trang trí hoặc xóa album, và được xóa khi xóa toàn bộ tài khoản HYTCG.
- Trang trí đã lưu hiện trong Collection, chi tiết và album. Ảnh ghép artwork riêng giữ tên/series/CardId/rarity do native cấp và có nhãn **CUSTOM**. Studio không sửa model, rarity, giá trị phân giải hay ảnh gốc trong `.hycard`; chỉ native được trừ gems/vật phẩm cho giao dịch.
- Album, trang trí, XP và gắn kết gắn với **CardId**, nên nâng cấp giữ lại dữ liệu của thẻ chính; nguyên liệu/phân giải trở thành ô trống trong album và không còn hiện trang trí. Thẻ đang thám hiểm vẫn xem và đặt vào album được. Hoàn tất nâng cấp hoặc nhận thưởng thám hiểm trước khi sửa trang trí, huấn luyện, hẹn hò, nâng cấp hoặc phân giải thẻ đang bận.

### Worker và thám hiểm

Nhân vật sở hữu đóng vai trò worker: chọn **1–3 thẻ** đang sẵn sàng, chọn tuyến, kiểm tra tổng **Effort / Nỗ lực**, rồi **Bắt đầu thám hiểm**. Effort, ATK, DEF và HP do native tính từ nhân vật, rarity, level và bond; tăng XP hoặc gắn kết giúp đội mạnh hơn. Thẻ legacy chỉ dùng để xem.

| Tuyến | Thời gian | Effort tối thiểu | Gems cơ bản | XP mỗi thành viên | Vật phẩm |
| --- | ---: | ---: | ---: | ---: | --- |
| Đồng cỏ ánh trăng | 5 phút | 12 | 8 | 20 | 1 sách + 2 fiber |
| Đài thiên văn ánh sao | 20 phút | 30 | 16 | 35 | 1 bộ trà + 2 petal |
| Di tích trên mây | 60 phút | 65 | 30 | 60 | 1 ruy băng + 2 crystal |

Mỗi ngày bắt đầu tối đa **6 chuyến**, đồng thời tối đa **3 chuyến chưa nhận thưởng**. Gems cộng thêm `min(floor(gems cơ bản / 2), floor(Effort đội / 20))`. Đóng plugin/launcher vẫn tính thời gian chuyến đi. Khi hết giờ chọn **Nhận / Claim** để nhận gems, XP và vật phẩm đúng một lần; đội vẫn bận cho đến khi nhận thưởng.

### Túi đồ, PvE và gắn kết

- **Túi đồ**: quản lý vật phẩm tiêu hao, nguyên liệu và dye trong một màn hình; có tìm kiếm và bộ lọc theo loại. Chọn nhân vật để dùng vật phẩm hoặc chuyển sang Studio để áp dụng dye. Sách huấn luyện cho **80 XP**; bộ trà và ruy băng mở hoạt động hẹn tương ứng, dùng chung giới hạn Gắn kết. **Fiber**, **Sakura petals**, **Arcane crystal** vẫn được nhận từ thám hiểm và giữ số lượng hiện có; hiện là mục chỉ xem, không dùng để tạo khung hoặc tăng XP. Khi khởi tạo gameplay nhận **3 sách, 3 bộ trà và 1 ruy băng** một lần; ba nguyên liệu bắt đầu từ 0. Mỗi loại giữ tối đa **999**; kho đầy vẫn nhận được phần thưởng gems/XP của chuyến đi. Vật phẩm không tự nạp lại hằng ngày.
- **Đấu trường theo lượt**: chọn đội **1–3 nhân vật**, bắt đầu rồi chọn kỹ năng ở mỗi lượt. Các thành viên còn HP thay nhau hành động, đối thủ phản hồi sau từng kỹ năng; có animation đánh, hồi phục, phòng thủ và HP/energy. Có **20 chặng**, tối đa **60 lượt/trận**, **3 trận/ngày**. Trận đang diễn ra được lưu local và khóa đội; có thể đóng/mở plugin để tiếp tục hoặc chọn Rút lui. Kỹ năng mở theo cấp: Strike/Guard **Lv1**, Mend **Lv3**, Burst **Lv5**, Focus **Lv8**, Pulse **Lv12**, Nova **Lv18**. Menu Kỹ năng trang bị tối đa 4, bắt buộc giữ Strike miễn phí; energy tối đa 6. Thắng lần đầu chặng `n` nhận `8+n` gems, thắng lại nhận `4+floor(n/2)`; mỗi thành viên nhận `20+2n` XP. Thắng nhận một sách huấn luyện, hoặc một ruy băng ở chặng chia hết cho 5. Thua nhận **5 XP mỗi thành viên**, không mất thẻ.

Daily bổ sung **hoàn tất và nhận thưởng 1 chuyến thám hiểm**, **thắng 1 trận Arena**, **tặng quà 1 lần**; mỗi nhiệm vụ **30 gems**, tự nhận đúng một lần/ngày cùng giao dịch gameplay. Nhiệm vụ game/movie trước đây giữ nguyên; ngày mới lúc **00:00 UTC+7**. Các bối cảnh Explore, Arena và Bond dùng SVG local tĩnh.
- **Gắn kết / Dating**: chọn nhân vật rồi **Trò chuyện**, **Uống trà** hoặc **Tặng quà**. Trò chuyện miễn phí cho **12–18 bond + 5 XP**; trà dùng một bộ trà cho **22–30 bond + 10 XP**; quà dùng một ruy băng cho **40–55 bond + 15 XP**. Tối đa **6 hoạt động/ngày** toàn tài khoản và **2 hoạt động/ngày/thẻ**. Bond tối đa **1000**; các mốc **40/120/250/500/800** tăng cấp gắn kết và chỉ số.

Level tối đa **30**, XP tích lũy tối đa **26.100**; XP yêu cầu để đạt level `L` là `30 × (L−1) × L`. Các giới hạn ngày đổi lúc **00:00 UTC+7**. Ví, inventory, tiến trình và biên nhận thưởng cùng nằm trong vault được bảo vệ; native xác thực quyền sở hữu/ảnh trước mỗi thao tác. Xóa tài khoản HYTCG xóa album, trang trí, gameplay và artwork riêng cùng các thẻ; launcher chặn thao tác xóa khi còn request mở thẻ, lưu hoặc Atelier đang chạy.

Catalog/layout và ảnh khung của phiên bản trước chỉ còn phục vụ kiểm tra tương thích dữ liệu cũ. Thêm hoặc sửa các tài nguyên này không tạo mục mua/gắn khung trong app. Thiết kế khung đang dùng được dựng theo rarity trong Canvas và native compositor; Studio chỉ tùy biến dye và artwork.

## File và cấu hình

```text
dist/
  HYROLauncher.exe
  assets/apikey/
    zerochanuser.txt      # Username Zerochan, không hiển thị trong panel
    hytcgconfig.json      # Chỉ cấu hình nguồn dữ liệu/retry/concurrency
  assets/hytcg/frames/    # Tài nguyên khung cũ phục vụ tương thích; không còn shop/editor
  hytcg-vault.dat         # Ví, ownership, album, cosmetic, gameplay và biên nhận; DPAPI
  hytcg-vault.stamp       # Dấu hiệu đã khởi tạo, phát hiện vault bị xoá/thay
  cards/
    SSR_Character_Name_20261008_XXXXXXXXXXXX.hycard
  hytcg-previews/         # Preview DPAPI + HMAC, có thể dựng lại; tối đa 256 file / 32 MB
  hytcg-artworks/         # Artwork Studio chuẩn hóa, DPAPI + ràng buộc tài khoản; tối đa 64 .dat
  .cache/hytcg-characters/ # Metadata AniList cache theo đúng nhân vật + tên + series
```

Username nằm trong `assets/apikey/zerochanuser.txt` (`YOUR_ZEROCHAN_USERNAME`); cấu hình API nằm cùng thư mục ở `hytcgconfig.json`, tương tự các file client/secret khác của launcher. Bản chạy đọc từ `dist/assets/apikey/`. API config chỉ chứa phạm vi ID, retry, concurrency và timeout; sửa JSON không thay được odds, hồi lượt, gems hay giá mua.

| Rarity | Tỷ lệ cố định | Gems phân giải |
| --- | ---: | ---: |
| Common | 65% | 1 |
| Uncommon | 22% | 3 |
| Rare | 7,3% | 8 |
| SR | 4% | 20 |
| SSR | 1,1% | 60 |
| UR | 0,6% | 160 |

Odds tham khảo xác suất cơ bản của [Genshin Wish](https://webstatic-sea.hoyoverse.com/genshin/event/e20190909gacha/index.html?gacha_id=fecafa7b6560db5f3182222395d88aaa6aaac1bc&lang=en&region=os_asia): UR 0,6%, SR+SSR tổng 5,1%. HYTCG chia thêm các rarity thấp và áp dụng riêng các mốc bảo hiểm **50/100/500**. Chính sách tập trung tại `HyTcgRules.cs`; bộ đếm nằm trong vault được bảo vệ. Bộ đếm mới bắt đầu từ 0 khi cập nhật từ phiên bản chưa có pity.

Các field API: `CharacterIdMin/Max` (phạm vi random local; mặc định hiện tại **1–1.000.000**, bao gồm hai đầu), `MaxCharacterAttempts` (mặc định 15, tối đa 20), `Concurrency` (1–3, mặc định 2), `RequestTimeoutSeconds`, `RetryDelayMs`. 1.000.000 là trần thử ID của app, không phải số nhân vật hiện có; các ID trống vẫn retry theo ngân sách. Rarity chỉ được chọn ở native service từ odds biên dịch, trước khi lấy nhân vật, bằng RNG của hệ điều hành.

PNG của thẻ mới có kích thước **750 × 1050**, kèm chunk `tEXt` HYTCG để nhận diện. PNG ngang **1050 × 750** của phiên bản trước vẫn được xác thực và giữ nguyên trong container; chỉ ảnh hiển thị được dựng lại thành dọc. Metadata trong PNG không quyết định số gems. Collection lấy model do native cấp từ vault, kiểm tra hash/chữ ký rồi mới hiển thị. PNG bị sửa/đổi tên/copy vào thư mục không tự tạo quyền sở hữu mới. File/chữ ký card cũ được giữ nguyên; trường orientation/revision mới không làm vô hiệu chữ ký phiên bản trước.

PNG của phiên bản cũ được nhập **một lần**, giữ để xem và được che nhãn nguồn cũ trên preview; không phân giải vì trước đây chưa có bằng chứng sở hữu chống sửa. Migration giữ nguyên bytes ảnh trong `.hycard`, chỉ xóa PNG cũ sau khi container và vault đã commit và đối chiếu thành công. Tombstone tồn tại ngay cả khi ảnh được chép lại sau phân giải.

## Bảo vệ local và giới hạn

`SHA-256` là hash một chiều; `HMAC-SHA256` xác thực hash container ảnh + model do native cấp bằng khoá random riêng. Ví/ownership/reservation/khoá, album, trang trí, kho dye, bản ghi khung cũ, inventory và gameplay được bảo vệ bằng **Windows DPAPI CurrentUser** và ghi atomic trong một file local; không database, backend hay cloud. Giao dịch Studio dùng sequence native và giữ 128 biên nhận gần nhất: retry khớp trả cùng kết quả, sequence cũ đã bỏ biên nhận bị từ chối thay vì trừ tiền lại. Artwork Studio nằm trong `.dat` local được mã hóa, kiểm tra hash và ràng buộc khoá tài khoản HYTCG; snapshot chỉ chứa ID/metadata, ảnh được đọc riêng khi cần. Corrupt/mất vault sau khởi tạo báo lỗi, không âm thầm reset balance. Khoá gắn với tài khoản Windows, cần sao lưu cả vault/stamp/cards/hytcg-artworks nếu chuyển thư mục trên cùng máy. Preview là cache có thể dựng lại.

Không thể cam kết chống gian lận tuyệt đối trong chương trình hoàn toàn local: người có quyền sửa executable, can thiệp tiến trình, đổi đồng hồ tiến tới hoặc phục hồi toàn bộ bản lưu cũ có thể vượt giới hạn. Cơ chế này chống chỉnh file/balance/card thông thường và redeem lặp; không phải chống cheat qua server. Ảnh `.hycard` được mã hóa theo tài khoản Windows và không preview được bằng trình xem ảnh thông thường. File PNG cũ được chuyển container giữ nguyên ảnh và metadata đã ký.

## Hiệu năng và thông tin nhân vật

- Màn hình loading native hiện ngay khi cửa sổ mở, trước khi WebView2 sẵn sàng; chỉ đóng sau khi giao diện nhận state và vẽ xong. Không tạo thêm WebView cho splash.
- WebView link/feed khởi tạo khi dùng lần đầu; view ẩn được giảm memory target và suspend khi phù hợp. MP3 luôn giữ playback khi ẩn, kể cả volume bằng 0. Main BGM/link đang phát audio không bị suspend. Pet dừng animation/GIF khi launcher ẩn và khôi phục lúc mở lại.
- Collection chỉ đọc ảnh trang hiện tại. Preview được bảo vệ DPAPI + HMAC ràng buộc hash của ảnh gốc; RAM preview tối đa 8 MB và ảnh chi tiết tối đa 16 MB. Cache không thay thế kiểm tra ciphertext của thẻ; thẻ sửa cùng kích thước hoặc timestamp vẫn bị từ chối.
- Thumbnail dùng **PNG giữ alpha** ở các góc bo tròn; phiên bản cache mới loại bỏ preview cũ có nền đen. Album không thêm nền đen hoặc bóng riêng sau thẻ: túi trong suốt dùng màu trang đang chọn.
- Native ghép trang trí vào ảnh hiển thị, không ghi lại `.hycard`. Cache compositor dùng SHA-256 của ảnh native, artwork và toàn bộ thuộc tính hiển thị; tối đa **64 thumbnail / 12 ảnh lớn**, tổng **32 MB RAM**. Mỗi lần đọc vẫn kiểm tra container thẻ và artwork trước khi dùng cache; xóa tài khoản cũng xóa cache. Các request Atelier chạy tối đa **2** đồng thời, tối đa **8** request chờ/đang chạy; bản xem UI tải tối đa **2 ảnh** đồng thời.
- Mystic chỉ vẽ các thẻ đang nhìn thấy, ưu tiên thẻ chi tiết và tối đa **16 thẻ** hoạt động. Canvas chạy **24 FPS**, giảm còn **16 FPS** khi hệ điều hành yêu cầu giảm chuyển động. Chuyển động vẫn do công tắc hiệu ứng HYTCG quyết định; cửa sổ/tab ẩn hoặc không có thẻ hiển thị thì dừng vòng vẽ.
- Xem chi tiết gọi AniList public GraphQL để lấy favourites, description, tuổi, giới tính, sinh nhật và link nhân vật. So khớp tên/alias và series chính xác; trường hợp tên mơ hồ hoặc series khác không nhận metadata/bonus.
- Cache thành công và kết quả không tìm thấy theo từng nhân vật để lần sau không gọi lại. Cache AniList được bảo vệ local; index favourites đọc một lần vào RAM. Sự cố mạng không bị ghi nhầm thành “không tìm thấy”. Không gửi client secret lên API public này.

## Flow và giới hạn API

1. Random rarity và `character_id` local.
2. Gọi [ACDB character API](https://www.animecharactersdatabase.com/api_series_characters.php) bằng `character_id`, qua `curl.exe` của Windows.
3. Kiểm tra ID, tên và ảnh profile; ID không tồn tại/JSON không hợp lệ được thử lại trong ngân sách.
4. Search [Zerochan](https://www.zerochan.net/api) bằng giao hai tag tên nhân vật và series đã URL-encode (`/Name,Series?json&l=50`). Nếu JSON lỗi HTTP 5xx/không hợp lệ, dùng API XML/RSS chính thức trên cùng truy vấn; XML không cho phép DTD/entity ngoài. Nếu không còn kết quả khớp sau lọc, thử tên với `strict`; tối đa hai truy vấn (mỗi truy vấn có một lần thử XML) và bốn chi tiết artwork. Kiểm tra tag nhân vật và series, hỗ trợ đảo họ/tên và các alias series được khai báo, bỏ qua kết quả explicit/suggestive. Không ghép series bằng substring; không xác minh được thì dùng ảnh profile. Khi ACDB không cung cấp series, chỉ tìm theo tên strict.
5. Nếu thiếu username, API/ảnh Zerochan lỗi hoặc không có kết quả, dùng ảnh profile ACDB. Nếu browser không decode được artwork, native bridge cũng hỗ trợ fallback profile.
6. Canvas dựng card và gửi PNG đến native để kiểm tra ảnh/kích thước, chuẩn hoá, nhúng metadata rồi ghi file atomically.

Zerochan yêu cầu User-Agent gồm tên app và username. Cấu hình theo tài khoản đã khai báo: `YOUR_ZEROCHAN_USERNAME`. Native đặt `HYTCG - YOUR_ZEROCHAN_USERNAME` và giãn các lượt API tối thiểu 1.1 giây, kể cả redirect, theo giới hạn 60 request/phút của Zerochan. Không cần mật khẩu hoặc API key.

Theo [tài liệu API ACDB](http://wiki.animecharactersdatabase.com/index.php?title=API_Access), từ 04/01/2026 ACDB yêu cầu cookie hoặc một client thuộc whitelist, và giới hạn 1 request/giây. HYTCG chạy **curl.exe thật của Windows** với User-Agent `curl HYTCG/1.0 (HYRO Launcher; ACDB API)`; không giả danh crawler, không giải browser challenge hoặc thu thập cookie. Không cần cài backend hoặc thư viện bổ sung trên Windows có sẵn curl.

Một hàng đợi native chung giãn mọi request ACDB (JSON, ảnh profile và redirect) tối thiểu **1.1 giây**, kể cả khi mở x10 hoặc retry ảnh. Zerochan vẫn chạy song song theo giới hạn riêng của họ. Curl chạy ẩn, không qua shell, bỏ qua curlrc, chỉ dùng HTTPS trên host ACDB được cho phép; native kiểm tra từng redirect, timeout và giới hạn bytes.

ACDB lỗi kết nối liên tiếp 3 lần sẽ dừng sớm. Nếu trả HTML `Checking browser…` với HTTP 200 hoặc 503, plugin nhận diện và báo `AcdbBrowserCheck` ngay, không coi đó là ID không tồn tại rồi thử thêm 15 lần. Pack cũng ngừng gửi các card chưa bắt đầu khi gặp browser check/rate limit/thiếu curl; card đã hoàn thành vẫn được giữ. Mỗi card có deadline 60 giây. Khi hết ngân sách ID mà không tìm được nhân vật hợp lệ, lượt mở được tiêu thụ và bồi **160 gems** một lần, ô kết quả hiện `+160 gems`; không tạo thẻ giả và không có Retry cho ô này. Lỗi mạng, rate limit, browser check, ảnh hoặc hủy session vẫn hoàn khoản đã trả theo cơ chế cũ.

## Bridge và filesystem

- Renderer không có Node hoặc quyền ghi filesystem; không gọi HTTP trực tiếp (`connect-src 'none'`).
- Native chỉ nhận lệnh HYTCG từ đúng document local `ui/hytcg.html?id=builtin.hytcg&v=<build>`, đúng cửa sổ và session. URL/CSS/JS có phiên bản để tránh giữ trang cũ.
- API dữ liệu và ảnh mở thẻ/nâng cấp vẫn dùng endpoint cố định và host HTTPS ACDB/Zerochan được cho phép. **Studio là ngoại lệ có chủ đích**: người dùng có thể nhập ảnh từ host **HTTPS công khai bất kỳ**, cổng **443**, không username/password. Native chặn localhost, IP private/link-local/special và các mạng chuyển tiếp IPv6; kiểm tra toàn bộ kết quả DNS rồi kết nối tới IP đã kiểm tra, trong khi TLS xác thực hostname ban đầu. Tối đa **3 redirect**, từng redirect kiểm tra lại URL/DNS/TLS; timeout từng thao tác **15 giây**, tổng **45 giây**, response tối đa **8 MB**. Không dùng proxy, cookie hoặc credential; response chỉ nhận ảnh rồi kiểm tra PNG/JPEG thực. Artwork chuyển thành data URL để Canvas không bị CORS taint.
- Save chỉ nhận `cardId` đã được native cấp và authorize vào reservation, PNG đúng kích thước của orientation do native xác định; renderer không được chọn đường dẫn, rarity, metadata hoặc số gems để ghi. Upgrade Save chỉ nhận token intent và PNG, dùng toàn bộ model/rarity/nguyên liệu native.
- Tên file được làm sạch, không ghi đè card khác, không đi theo junction/symlink của thư mục cards. Config JSON local là cấu hình, không chứa collection.
- Đóng plugin huỷ request của session; lượt chưa bắt đầu được trả lại, card đã cấp còn pending để tiếp tục nhận.

## API Monitor

Chuột phải icon launcher ở system tray → **API Monitor**. Capture chỉ bắt đầu khi mở cửa sổ, dừng ngay khi đóng; mở lại tạo file mới. Theo dõi các request native của Steam Library/Store/Explore/Backgrounds, Twitch, IGDB, ACDB, Zerochan và poster HyBrowser, cùng quyết định retry/fallback/ALT của HYTCG. Log có thời gian, endpoint, method, HTTP status, duration và kích thước response. Không ghi body, header, token hay secret; các query nhạy cảm được che. Log không phải lịch sử duyệt web của các website trong HyBrowser.

File UTF-8 TSV nằm trong `dist/logs/api-<timestamp>-<session>.tsv`; nút **Thư mục log** mở thư mục này. Bộ lọc nguồn và cuộn tự động chỉ điều khiển màn hình, file vẫn ghi mọi nguồn của phiên đang mở. Giữ tối đa 2.000 entry trong RAM và 1.500 dòng hiển thị; file giữ toàn bộ phiên. Request bắt đầu khi cửa sổ đóng hoặc thuộc phiên trước không được ghi vào phiên mới.

## Kiểm thử

Các kiểm thử reflection dùng `dist/HYROLauncher.check.exe`; tạo bằng `powershell -NoProfile -File build.ps1 -ExecutableName HYROLauncher.check.exe`. Sau kiểm thử, `clean-project.ps1` xóa build kiểm thử và output cũ, giữ dữ liệu runtime và báo cáo `output/verification/latest`. Dùng `-KeepCheckBuild` nếu còn cần chạy kiểm thử reflection.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-native.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-economy.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-shapes-upgrades.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-creative.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-rarity-rim.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-gameplay.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-adventure.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-artwork-download.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-lifestyle-import.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-lifestyle-bridge.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-artwork-live.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-panel.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-api-monitor.ps1
node tests/verify-hytcg-ui.cjs
node tests/verify-hytcg-hover.cjs
node tests/verify-hytcg-progression-ui.cjs
node tests/verify-hytcg-rarity-only-ui.cjs
node tests/verify-hytcg-studio-position-ui.cjs
node tests/verify-hytcg-mystic-ui.cjs
node tests/verify-rarity-rim-ui.cjs
node tests/verify-hytcg-adventure-ui.cjs
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-integration.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-legacy.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-production-migration.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-performance.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-launcher-performance.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-character-info.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-open-compensation.ps1
node tests/verify-hytcg-enrichment-ui.cjs
powershell -NoProfile -ExecutionPolicy Bypass -File tests/verify-hytcg-live.ps1
```

Native test dùng transport fixture, kiểm tra RNG, ID/retry, fallback, huỷ request, save an toàn, metadata Unicode, filter/paging Collection, chuỗi 500 lượt bảo hiểm và các giao dịch nâng cấp/phân giải. Creative/gameplay/compositor test tạo account riêng dưới `output/verification`, kiểm tra persistence, khoá thẻ, biên nhận thưởng, PNG/JPEG giới hạn, viền của sáu rarity và dye chỉ đổi viền. Bridge test kiểm tra tạo/mua/gắn khung đã bị từ chối và dữ liệu cũ vẫn đọc được. Import test reflection dùng assembly build thực trên account fixture, không mở UI hay gọi mạng; downloader test kiểm tra parser HTTP, chính sách URL/IP và cancellation bằng fixture offline. UI test dùng Edge headless và bridge fixture, kiểm tra Studio không còn editor/selector khung, căn chỉnh artwork/quote/cache/reset, dye/artwork/sketch, album trong suốt và Mystic chuyển động gắn với thẻ; không ghi card kiểm thử vào thư mục collection thật. Migration test sao chép ví/stamp/PNG dưới mutex rồi kiểm tra phiên bản mới trên bản sao, giữ nguyên production. Live probe gọi API thực và lưu artwork kiểm thử chỉ trong `output/verification/hytcg-live`.

Mã chính: `HyTcgService.cs` (API/RNG/config/PNG), `HyTcgRules.cs` (odds/giá trị), `HyTcgEconomy.cs` (ví/integrity/giao dịch), `HyTcgCreative.cs` (album/Studio economy/artwork và tương thích khung cũ), `HyTcgFrameCatalog.cs` (đọc/xác thực catalog và layout cũ), `HyTcgGameplay.cs` (worker/inventory/PvE/bond), `HyTcgCosmeticRenderer.cs` (khung theo rarity, căn chỉnh artwork và ảnh trang trí native), `HyTcgArtworkDownload.cs` (HTTPS import artwork), `HyTcgLifestyleActions.cs` (bridge Atelier), `HyTcgAcdbClient.cs` (curl/rate limit), `HyTcgActions.cs` (bridge), `ui/hytcg-renderer.js` (card), `ui/hytcg-rarity.js` và `ui/rarities/*.svg` (emblem/màu rarity cố định), `ui/hytcg-effects.js` và `ui/hytcg-effects.css` (Mystic), `ui/hytcg.js` và `ui/hytcg.css` (giao diện), `ui/hytcg-lifestyle.js` và `ui/hytcg-lifestyle.css` (Atelier).
