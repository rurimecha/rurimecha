# 用户、角色与会话

首次安装从初始密码文件建立 admin 管理员。没有公开注册。服务器授权生效后，管理员可创建用户。用户目录最多容纳 1000 个账户；用户名区分大小写且唯一，1..80 UTF-8 字节、首尾无空白/控制符。创建/重置密码为 8..1024 UTF-8 字节。凭据文件不是 API Bearer。

## 权限

所有账户共享媒体库和只读 DAV 来源目录。admin 管理用户、媒体来源、作品资料、扫描/归纳/刮削/探测、全局设置、日志和 MCP。member 只读浏览作品、目录、人物/社团/标签、资源/图片/字幕/媒体信息及播放协商，可以解析队列、播放、维护自己的账户/会话/偏好/播放列表/自定义收藏集/进度/书签。未明确列入普通用户允许范围的新管理路由默认要求管理员；拒绝返回 403 forbidden。

归档收藏集共享且只读；日常读取和播放中的个人记录按认证账户隔离。仅管理员可以使用[系统迁移包](migration.md)迁移所有账号的个人数据。公开数据库实体身份不因用户名改变。当前没有来源级可见范围、公开分享或注册。

## 管理用户

- `GET /users`：管理员目录，返回 items，最多 1000；无密码材料。
- `POST /users`：`{ "username":"alice", "role":"member", "password":"..." }`，201 及当前账户 ETag。
- `PUT /users/{user}`：同上完整替换；password 空字符串保留密码。要求 `If-Match: "<user-id>:<revision>"`。
- `DELETE /users/{user}`：同一 ETag，204；级联删除该用户的个人记录和会话，保留全库资料与源媒体。

- `PUT /users/{user}/access` 完整替换 `{"disabled":false,"allow_remote":false}`，要求当前账户 If-Match，返回账户和 ETag。新账号默认启用并仅允许局域网访问。账号响应包含 `builtin`、`disabled`、`allow_remote`。

内置管理员由固定账户 ID `admin` 识别，用户名可以修改。此账户不能禁用、删除或降权。停用账号撤销全部会话并关闭密码直连。重启用账号后需重新登录。角色变更和重置密码也会撤销受影响账号的会话。重名或保护内置账号返回 409 `account_conflict`，修订过期返回 412。

## 自身账户及认证

`POST /auth/login` 输入 username/password，可选 client_name；返回 user、access_token、token_type=Bearer、expires_at、session。浏览器同源响应设置 HttpOnly/SameSite=Strict Cookie，客户端不把令牌保存在网页存储或 URL。所有写 Cookie 请求必须有同源 Origin/Referer。原生/外部客户端使用 Authorization: Bearer。登录计算有界、限速，429 带 Retry-After。

`GET /auth/me` 返回当前真实用户及 ETag（ID 不固定为 admin）。`PUT /auth/me` 完整输入 username/current_password 并提供 ETag；`POST /auth/password` 输入 current_password/new_password 并提供相同 ETag。修改自身密码撤销自己的全部会话；改名保留会话。`POST /auth/logout` 撤销当前会话并清 Cookie。

`GET /auth/sessions` 仅列出自身未过期会话（无令牌），标记 current。`DELETE /auth/sessions/{session}` 只撤销自己的一个会话；`POST /auth/sessions/revoke` 只撤销自身全部会话。会话固定有效 30 天，每账户最多 256 个，不自动挤掉其他设备。未授权访问 401；长媒体连接每分钟复查账号、会话和网络访问资格。

未激活或授权失效时，只有内置管理员可以登录和访问。此规则覆盖此前签发的会话以及 WebDAV 密码直连。授权失效后重新激活，其他用户需重新登录。正确凭据的登录遇到停用、授权失效或远程访问限制时分别返回 403 `account_disabled`、`account_license_required` 或 `remote_access_denied`。既有令牌失效返回 401。客户端可以更换网络地址，仍须符合 [网络访问规则](network.md)。

账户结构属于当前开发 v1；结构签名不同明确拒绝，没有迁移、历史单管理员协议分支或自动清库。
