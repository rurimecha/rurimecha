# 产品授权管理

单档授权按安装实例绑定。服务器和 Android 本机库分别向授权中心激活和验证，服务器实例内的账户共享该服务器授权。证书表示产品 `rurimecha` 在有限期内已获授权，不携带功能清单、程序版本或数据库结构摘要。调整功能范围不会导致既有证书失效。服务器上的珍藏片段、AI 和智能选路需要有效服务器授权。Android 本机库的珍藏片段和 AI 需要有效本机授权。普通媒体播放与下载、多用户和更多声优依照现有账号访问规则。

## Core 管理接口

均在 `/api/v1` 下。状态可由所有已登录账户读取；激活和反激活仅 admin 可调用。网页使用现有同源 HttpOnly Cookie；写请求仍检查 Origin/Referer；原生使用 Bearer。

| 方法与路径 | 请求及响应 |
| --- | --- |
| `GET /license` | `state`、`configured`、`capabilities`，已有可验证证书时附 `customer`、`license_id`、RFC3339 `expires_at`。管理员还可读取 `device_type`、已登记的 `system_device_name` 以及可用的 `suggested_system_device_name` |
| `POST /license/activate` | `{"license_key":"…","device_name":"家里的服务器","system_device_name":"homeserver"}`。密钥 32..256 字节，备注 1..80 个 Unicode 字符。`system_device_name` 可选、最多 80 字符，空字符串清除登记的设备名。成功返回当前状态 |
| `POST /license/deactivate` | 必须提交空对象 `{}`；返回 `{"ok":true}` |

状态 `active` 表示有效，其余为对应 `license_*` 代码。`capabilities.multi_user`、`capabilities.ai`、`capabilities.treasured_clips` 和 `capabilities.smart_routing` 表示当前服务器授权能力。手机本机授权含 AI 和珍藏片段。允许增加其他键，客户端应忽略未知键。

激活及续期需要 Core 连接发行方激活服务。同一许可证、同一实例密钥重复激活复用绑定名额，可在线更新期限和宿主机信息；到期日由云端许可证决定，重新激活不会重置期限。激活密钥只用于本次请求，不在 Core 持久保存或写入日志。超时后可安全重试。

更换其他许可证前必须先反激活，`license_deactivation_required` 为 409；更新同一许可证不需反激活。

反激活先持久保存停用状态，再联网释放名额。网络失败后 `state=license_deactivation_pending`，增值功能已停用，管理员应重试反激活。云端成功而响应丢失也可重试，不重复释放其他绑定。反激活不要求旧证书仍未到期或宿主机仍匹配。云端释放不能通知离线副本，旧副本可能使用至既有证书到期。

## 错误

保持公共 JSON 错误结构。`license_required`、`license_expired`、`license_invalid`、`license_clock_error`、`license_host_unavailable`、`license_device_changed`、`license_deactivation_pending` 为 403；反激活待完成时再次激活为 409。`license_key_invalid`、`license_revoked`、`license_activation_limit` 为 403，`license_invalid_input` 为 400，`license_not_configured`、`license_storage_error` 为 503，`license_service_unavailable` 为 502。授权错误不等于账户 401，不得因此退出账户或删除个人资料。

## 云端授权中心

服务地址以发行配置为准。详细路由和 DTO 见 [独立 OpenAPI](license-service.openapi.json)。Core 与中心之间只在激活、更新、反激活时联网。

机器客户端应发送真实应用 User-Agent，例如 `RuriMecha-Core/1`；workers.dev 的浏览器完整性检查可能拒绝通用库默认标识。不能把 User-Agent 当作授权证明，权限始终依赖管理身份、密钥或实例签名。

- `GET /health` 检查发行密钥和结构；`GET /public-key` 返回公开的 product/issuer_url/public_key，用于发行配置，不代替客户端固定的信任根。
- 云端管理接口仅接受独立的 `Authorization: Bearer` 凭据。本机管理台负责管理员登录和云端调用。
- `GET|POST /admin/customers`、`PUT /admin/customers/{customer}` 维护购买用户资料；不表示已完成邮箱身份验证。
- `GET|POST /admin/licenses`、`PUT /admin/licenses/{license}` 管理单档许可证；创建时可选 order_ref 仅作人工登记，重复编号整体回滚，不执行付款确认或发邮件。
- `GET /admin/licenses/{license}/key` 需要管理员身份，返回当前 `{id, license_key}`，包括已停用或到期的许可证。许可证列表省略密钥。
- `POST /admin/licenses/{license}/key` 接受空对象并返回新密钥。原密钥此后不能激活，许可身份、证书和绑定保持不变。
- `GET /admin/activations?license_id=…` 分页读取历史，`POST /admin/activations/{activation}/release` 接受空对象并幂等释放指定绑定。`GET /admin/events` 分页读取事件，可选 license_id。
- 列表默认 limit=50，范围 1..100，offset>=0；用户和许可证支持最多 254 字符的 q 字面检索。许可证列表仅带本页活跃绑定，历史单独查询；items/total/limit/offset 或 licenses/activations/total/limit/offset 来自同一读取事务。
- `POST /v1/activate`、`POST /v1/deactivate` 保持前述签名与绑定语义。激活接受 license_key/device_name/instance_key/host_id、目标 `target`（`server` 或 `android`）及可选 existing_license_id。新 Core 请求携带目标，并验证新签发证书的目标与设备类型一致。已有无目标证书可按原宿主、实例密钥和期限继续离线使用，主动更新可换取带目标的证书。反激活接受 certificate/proof。
- `POST /v1/self`、`POST /v1/release` 始终返回 503 `self_service_unavailable`。丢失设备的名额可通过官网邮箱验证流程释放指定绑定，参见 [授权中心契约](license-service.openapi.json)。发行方管理员也可核对后处理。

授权中心错误对象含稳定 error.code 与 request_id。常见额外代码：customer_conflict/order_conflict 为 409；self_service_unavailable/license_schema_mismatch/license_not_configured 为 503；request_too_large 为 413；rate_limited 为 429。管理 PUT 为完整替换，按最后提交生效。停用、缩短期限和释放不能即时撤销离线副本，既有证书可能继续使用至原到期日。

创建和使用账号依照 [账号与会话](accounts.md)。产品授权失效时保留资料、账号和播放队列。

付费通行证在创建后七天内且从未激活时可办理退款，超过七天仍可激活。测试通行证在发放后七天内激活。同一许可证首次激活开始计算31天，重复激活及换机沿用原期限。激活期限已过返回 `license_activation_window_expired`，退款处理中返回 `license_refund_pending`，已退款返回 `license_refunded`，均为403。云端 `rate_limited` 由Core以429传递。

管理员可通过 `PUT /api/v1/license/server` 提交 `{"server_url":"https://license.rurimecha.top"}` 修改授权服务地址；仅接受无凭据、路径、查询或片段的 HTTPS 地址，空字符串恢复默认值。成功返回 LicenseStatus，新增 `server_url` 为当前地址。保存后立即生效并持久化，优先于启动参数 `-license-server`；不联网、不修改现有证书、身份及固定验签公钥。按成功保存顺序覆盖，无 ETag；普通用户禁止修改，沿用会话及同源写入校验。无效地址返回 400/license_server_invalid，保存失败返回 503/license_storage_error。
