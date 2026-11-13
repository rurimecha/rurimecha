# Ruri Scrape 文字元数据服务

服务使用独立账号和数据目录，默认地址 http://127.0.0.1:8091。当前为文字元数据缓存，不下载、存储或提供图片文件；封面、样图地址可以作为文字引用保留。所有路径以 `/api/v1` 为前缀，机器契约见 [OpenAPI](scrape-service.openapi.json)。服务与 Core 分别运行，Core 不提供本服务的采集、语料库或密钥管理接口。支持已识别的前一版数据库原地升级；未知结构和更高版本拒绝打开并保留原数据。

## 认证与连接测试

首次启动生成数据目录中的 initial-password（0600），用户名 admin。它是登录密码，不是 API 密钥。

POST /auth/login：`{"username":"admin","password":"...","client_name":"管理工具","client_kind":"browser"}`，返回 user/session/token，同时设置同源 HttpOnly、SameSite=Strict Cookie。浏览器写入校验 Origin/Referer；外部客户端使用 Bearer。client_kind 可省略（默认 other），支持 browser/android/desktop/other。会话响应含 client_kind 和 login_ip；后者为登录时的连接对端地址，不信任转发头，反向代理下可能是代理地址，不作设备身份。会话30天，可 GET /auth/sessions、DELETE /auth/sessions/{id} 撤销；POST /auth/logout 注销。PUT /auth/me 带 If-Match 更新 username/current_password/password，账户修订变化撤销既有会话与绑定旧修订的密钥。

POST /keys 创建 `{"name":"Ruri","scope":"lookup","days":365}`，token仅创建时显示；GET /keys 不回显，DELETE /keys/{id} 撤销。权限：read 仅本地已有资料，lookup 允许缺失抓取及显式刷新，admin 允许完整管理。GET /auth/access 返回当前凭据的 read/lookup/refresh/manage 布尔能力，不触发抓取，可用于连接和权限验证。

## 本地优先与语言、字段选择

GET /products/{code} 返回完整已保存 Record，存在即返回；`metadata.texts` 按语言保存，`metadata.product_info.localized_metadata` 保存各语言的完整商品字段、封面引用、来源时间和字段状态（结构见 metadata-scraping.md）；缺失且具有 lookup 权限时抓取入库。GET /products?q=...&limit=50&offset=0 只搜索已有资料，不发起抓取。POST /products/{code}/refresh 显式重新获取所选整份资料，失败保留成功记录。GET /products/{code}/package 导出 Core 可导入的 ruri-metadata 包，images 永远为空。

POST /lookup 接受语言优先级和所需字段，不使用用户的界面语言或音频语言推断资料文本：

```json
{"code":"RJ123456","languages":["zh-Hans","ja","en","und"],"fields":["title","description","product_info/price"],"refresh":false}
```

返回 `{code,revision,language,values,source,sections}`；values 仅包含所需字段，包含署名、标签、价格、封面等字段时也读取所选语言的快照，不混入其它语言商品字段。服务器按语言数组顺序查找，先检查已保存资料，缺少时尝试该语言的上游资料；完整结果优先；无法取得完整结果时，metadata 可返回有成功字段的部分观察，并通过 sections 如实报告缺项。不同语言文字仍分别保存，不合并成同一种语言正文。简繁中文标准区域标记规范化为脚本标记，日/英等区域标记规范化为其提供者支持的语言。支持 ja、zh-Hans、zh-Hant、en、ko、de、fr、es、und，最多12个。未指定时按设置中的 `default_return_locale` 优先选择（`zh-Hans` 或 `ja`），随后尝试日文和未知语言；响应 `language` 是所选请求观察的语言；`values.metadata.texts[].locale` 分别标记实际标题与简介语言，不能由请求语言推断正文。DLsite 文本采用当前商品的官方版本语言，缺少明确声明的资料统一使用 `und`。ASMR.one 无资料文本语言声明时也使用 `und`。

fields 最多64项，可选 metadata（该语言全部已知规范资料）、image_references（图片地址引用，不含图片文件）、title、description、description_parts、original_locale、release_date、release_date_precision、languages、identifiers、credits、tags、cover_ref、product_info，及 product_info/{key}。省略 fields 默认 metadata。未知字段、非法参数拒绝；0和false是有效值，null/缺失不视为已获得。空标题/简介只有在对应字段成功且确认为空时视为完成，不因解析失败推断为空。仅请求语言无关的字段时，返回 language 为空。metadata 的完成度按所需字段状态判断，存在标题不代表简介和其他所需字段已完成。整包保留本请求观察实际取得的各语言文本，不截断为请求语言，也不带入其他请求的无关译文。显式 title/description 选择仍限于所选实际文本语言。

source 保留提供者、编号、URL及时间；选择接口不返回原始响应。没有语言可满足条件时404。未取得的语言/字段组合缓存1小时，实际获取失败按 failure_seconds 缓存；refresh=true 绕过。成功的低优先级语言在同次请求中不重复抓取。文本按实际证据保存，证据不足时为 und；请求语言、音声语言和相同标题均不能证明文本为日文。没有自动翻译。

简单字段查询允许跳过无需的介绍页。DLsite JSON/AJAX各字段单独验证，ASMR.one补充只在需要时使用。现存完整资料优先；部分观察保留成功字段，只补取未完成且可重试的所需字段，并受次数及冷却限制。成功空简介不重复抓取，可选的未提供商品字段不导致无限重试。同编号/配置/语言/请求范围的并发抓取合并，单个等待者取消不终止其他等待者。

## 自动采集与容量

GET/PUT /settings 的策略仅需 max_products 和 daily_new_limit：

- max_products：1–1000000，默认50000，限制有效持久化作品数。
- daily_new_limit：0–10000，默认0关闭自动采集；例如100表示尽量成功新增100部，不是只检查100个编号。已有作品、失败和重复编号不占成功额度，按需查询与导入不占自动采集的每日额度。

GET /collection 返回 day/acquired/daily_limit/products/max_products。日期按服务器本地时间计算。POST /collection/run 创建一次采集任务。自动任务从新作列表逐页发现，必要时切换到另一已启用提供者，跳过已有与当天已获取编号；达到成功额度、当天日期改变、可用列表耗尽或分页安全上限时结束。不枚举数值号段。列表耗尽或失败会稍后重试，不保证上游每天实际存在足够新作品。

同一时刻只接受一个采集任务。每日成功记录和作品写入原子提交，重启恢复游标，不重复消费成功额度；新的一天重新发现。正常运行时每15分钟检查是否有待补足额度，已有活动采集不叠加。修改额度后，发布前仍检查当前限额。

容量满时按最早更新时间淘汰，暂不淘汰活动任务的待处理作品。相关资料和搜索记录一并清除，数据文件占用不一定立即缩小。降低容量若受活动任务阻挡则整体拒绝，不做部分修改。

## 资料管理与交换

不提供手工元数据编辑、字段保护或覆盖接口。POST /products 可导入单个当前格式文字包，已有作品409；有图片字节的包拒绝。DELETE /products/{code} 带 If-Match 删除，之后查询可重新获取。刷新、导入和上游合并负责更新资料；缺失或失败字段保留此前成功值，不保存重复的有效/自动副本。

GET /export?limit=500&offset=0 返回 ruri-scrape-bundle v1，包含资料和来源，不含账户、配置、密钥或图片。POST /import：`{"mode":"skip","expected_revisions":{},"bundle":{...}}`，mode 为 skip/fail/replace；replace 对每个编号提供本地字符串修订（从未存在为"0"），任一过期整体回滚。最多1000条/64MiB，验证后原子提交；导出每页独立快照，完整导出应暂停写入。

## 持久任务

POST /jobs：lookup 模式提交1–1000个不重复 codes，refresh 可选；collect 模式使用自动采集策略，不接受 codes。无图片参数或用户管理的发现来源/日历计划。

GET /jobs、/jobs/{id}、/jobs/{id}/items、/jobs/{id}/events 查看持久进度，列表分页 limit 默认50、1–500，offset>=0。POST /jobs/{id}/cancel 请求取消；POST /jobs/{id}/retry 仅补未完成项和字段，成功项保留，网络配置取当前快照。主动 refresh=true 则重新获取所选完整资料，不能被已有部分成功的标题阻止；显式窄字段刷新仍只修改所请求字段。DELETE /jobs/{id} 仅删除终态任务及明细，不删除资料。重启后 running 恢复排队，canceling 变为 canceled；已保存单项和分页检查点不重复处理。

## 设置与错误

providers 包含 dlsite/asmr.one 各一次，可分别启用；DLsite 优先。metadata_locales 独立控制自动获取的简体中文、繁体中文、日文、英文，至少开启一种；默认仅日文。default_return_locale 设置未指定语言时的返回优先级，可选 zh-Hans 或 ja；timeout_seconds 默认30（5–120）；interval_ms 默认1000（100–60000）；failure_seconds 默认30（0–3600，0关闭实际获取失败的冷却）。proxy_url 写入专用，省略保留、空串清除，只返回 proxy_configured，不回显地址或认证。设置、删除和账户更新用字符串修订/If-Match，过期或缺失409。

最终抓取失败及其失败缓存命中统一404 not_found，不向查询者区分上游失败原因；强制刷新可重试。已有成功数据仍可读取。语法错误400、未认证401、无权限403、容量413、忙429、内部错误500，不将这些服务管理错误当作不存在。所有业务响应 no-store。

## 日志与详细诊断

管理员在独立管理页的「设置」中开启「详细诊断」并保存，重现问题后关闭并保存。`GET/PUT /settings` 增加布尔 `diagnostics_enabled`，默认 false，持久化且保存成功后立即生效；仍使用完整设置替换与现有 If-Match 规则，缺失/过期返回 409。并发保存串行持久化与激活，失败不改变活动开关。关闭后停止新增诊断记录，已有记录按上限保留。

`GET /logs` 仅允许管理员会话或 admin 密钥，read/lookup 密钥返回 403。支持 level（debug/info/warn/error，或 attention 同时匹配 warn/error）、q（事件/字段忽略大小写子串）、request_id（精确关联）、before（排他正十进制字符串游标）、limit（1–200，默认100）。最新在前，返回 items/next_before/has_more，过滤在分页之前执行，非法或重复参数返回400。机器结构见 LogEntry/LogPage。前端默认 attention，可查看全部或诊断记录，原始字段收进详情。

普通成功访问、轮询、缺失资料和过期会话只在诊断开启时记录；登录失败、权限问题、实际失败及任务结果保留必要摘要。相同任务的失败项目按原因汇总，具体项目仍在任务页；持续调度/队列存储故障记录首次、最多每五分钟一次累计摘要和恢复。成功日志/健康查询不记录自身。请求记录含关联ID、模板路由、状态及耗时，不记录认证头、查询串、请求正文或密钥。

记录同时写入有界内存、标准错误和独立服务数据目录内的 logs/scrape.jsonl。最近5000条可查询，磁盘当前文件及3份备份每份约4MiB；保留期间跨重启恢复，不是无限审计历史。沿用凭据字段脱敏与截断。日志写入或数据库健康异常使健康检查失败，故障期间的内存记录不保证重启后仍在。

## 数据目录升级

当前数据库版本为2。首次使用当前程序打开已识别的v1库时，先生成包含已提交WAL内容的独立备份，再在一个事务中升级。账号、原登录会话、API密钥、设置、资料、任务和关联记录保留；既有会话的新增登录地址为空，不补猜历史信息。升级失败回滚，保留旧库和备份；再次启动可重试。未知签名或更高版本拒绝，不自动清空或创建替代库。升级后继续使用原数据目录即可。
