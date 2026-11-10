# 网络设置

`GET /settings/network` 仅管理员可读，返回设置、可选本机地址 `available_addresses`、当前访问地址 `urls`、实际局域网范围 `effective_local_networks`、发现可用状态 `discovery_available` 及 ETag。

`PUT /settings/network` 仅管理员可写，使用当前 ETag 作为 `If-Match`，完整替换：

```json
{"port":8080,"ipv4":true,"ipv6":false,"ipv4_addresses":[],"ipv6_addresses":[],"discovery":true,"local_networks":[],"allow_remote":false}
```

以上为初始设置。至少开启一种地址族。各地址族列表为空时监听该地址族的全部地址。非空列表填写本机单个 IP，可同时选择多个。端口范围 1–65535。修改监听设置会重启 HTTP 监听，现有连接可断开。新监听全部成功后保存设置。绑定失败时重新打开旧监听，返回 409 `network_bind_failed`；无法重新打开旧监听时返回 503 `network_restore_failed`，旧配置仍保存在磁盘。修订过期返回 412。

`local_networks` 接受 IPv4 和 IPv6 CIDR 列表。空列表使用当前监听地址对应的直连私有网段。回环始终视为本机。远程连接要求服务器及对应账号同时启用远程访问，限制覆盖网页、API、媒体和 WebDAV。来源按直接连接方判断，客户端提供的转发标头不会参与判定。代理入口需自行实施等效的来源访问限制。

局域网发现使用 DNS-SD `_rurimecha._tcp`，公布局域网内的访问地址和 HTTP 端口。发现结果仍需正常登录。Core 使用 HTTP 监听。
