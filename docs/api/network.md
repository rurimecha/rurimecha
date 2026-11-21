# 网络设置

`GET /settings/network` 仅管理员可读，返回设置、可选本机地址 `available_addresses`、当前访问地址 `urls`、实际局域网范围 `effective_local_networks`、发现可用状态 `discovery_available` 及 ETag。

`PUT /settings/network` 仅管理员可写，使用当前 ETag 作为 `If-Match`，完整替换：

```json
{"port":8080,"https_port":8443,"https_enabled":false,"force_https":false,"ipv4":true,"ipv6":false,"ipv4_addresses":[],"ipv6_addresses":[],"discovery":true,"local_networks":[],"allow_remote":false,"known_proxies":[]}
```

以上为初始设置。HTTP 监听始终保留。`https_enabled` 开启后增加 HTTPS 监听；`force_https` 仅在 HTTPS 已开启时可用，HTTP 请求以 307 重定向到同主机的 HTTPS 端口。HTTPS 请求不跳转。HTTP 与 HTTPS 端口范围均为 1–65535；同时监听时不可相同。至少开启一种地址族。各地址族列表为空时监听该地址族的全部地址。非空列表填写本机单个 IP，可同时选择多个。

首次启用 HTTPS 或更换证书时，PUT 请求可额外提交 `certificate_base64` 与 `private_key_base64`，两字段必须同时提供，各自解码后最多 256 KiB，内容为 PEM 格式证书链和匹配的私钥。省略时沿用已保存的证书。证书过期、未生效、格式错误或私钥不匹配返回 400 `network_certificate_invalid`，不能保存文件返回 503 `network_certificate_save_failed`。证书和私钥保存在应用数据目录的受限文件中；GET 仅返回 `certificate_configured`，不返回私钥或路径。关闭 HTTPS 不删除已保存证书；重新启用时可沿用，证书过期则需更换。HTTPS 地址中的主机名或 IP 必须与证书匹配，客户端也须信任签发者。

保存前校验证书并绑定新监听，成功后保存设置；绑定失败时重新打开旧监听，返回 409 `network_bind_failed`；无法重新打开旧监听时返回 503 `network_restore_failed`，旧配置仍保存在磁盘。修订过期返回 412。修改监听方式、地址或端口可能断开现有连接；HTTPS 证书更新同样重启监听。

`local_networks` 接受 IPv4 和 IPv6 CIDR 列表。空列表使用当前监听地址对应的直连私有网段。回环始终视为本机。远程连接要求服务器及对应账号同时启用远程访问，限制覆盖网页、API、媒体和 WebDAV。可信代理请求按还原后的客户端地址判断，其他请求按直接连接方判断，账号远程访问限制同样生效。

局域网发现使用 DNS-SD `_rurimecha._tcp`，公布局域网内的 HTTP 端口。发现结果仍需正常登录。`urls` 列出监听 IP 和端口；HTTPS 客户端应使用证书包含的名称或 IP。

## 可信反向代理

Core 自动信任直接连接的 IPv4/IPv6 回环地址，以及 Linux Docker 桥接容器内检测到的默认网关。同机 Nginx 通过回环地址或宿主机回环映射端口连接时无需配置。Docker 网关仅从容器内 veth 接口的默认路由获取，随网络策略定期刷新，不信任整个容器网段；host 网络模式不自动信任宿主机路由网关。检测不到网关时不扩大信任范围。

`known_proxies` 为额外信任的最多 128 个代理 IP 或 CIDR，默认空数组；空数组仍保留上述自动识别。仅填写管理员控制的代理地址，不接受域名、`0.0.0.0/0` 或 `::/0`。该设置保存后立即生效，不需要改变监听端口。PUT 省略该字段等同清空手动名单，不影响自动识别。自动地址不写入保存的设置或 GET 的 `known_proxies`。

只有直接连接方被自动识别或在手动名单中时，才处理 `X-Forwarded-For`、`X-Forwarded-Proto`、`X-Forwarded-Host`。自动识别仅允许还原最右侧的一跳；继续向左处理必须逐跳匹配手动名单，遇到不可信地址即停止。协议只接受 `http` 或 `https`，主机需为有效主机及可选端口；可信链中的无效值返回 400。Nginx 必须传递三个头；直接面向用户的单层代理应覆盖客户端传入值，例如 `X-Forwarded-For: $remote_addr`。多个代理的名单仅包含实际受控的中转节点，列表按各跳顺序追加。

还原后的协议与主机用于同源校验、HTTPS Cookie 和强制 HTTPS 判断；还原后的 IP 用于服务器及账号远程访问检查和登录记录。`Origin`、`Referer` 不得伪造或清除。HTTPS 入口可连接 HTTP 上游，Ruri 直接设置 Secure Cookie；代理无需改写 Cookie。多层代理应保留对齐的逐跳来源信息。当前只支持独立域名根路径，不支持子路径前缀。

完整配置见 [Nginx 教程](../USAGE.md#通过-nginx-访问-ruri)。
