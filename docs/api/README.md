# Ruri API 对接文档

这里供需要连接其他客户端或编写工具的开发者使用。安装、播放和日常管理请直接阅读[使用手册](../USAGE.md)，不需要调用 API。

接口地址为 `http://服务器地址:端口/api/v1`；使用 HTTPS 或反向代理时换成实际入口。请使用与所运行的 Ruri 版本配套的文档。先读取 `/info` 的 `api_version` 和 `capabilities` 判断接口支持情况，不根据应用版本号猜测能力。

## 开始接入

1. 使用已存在的 Ruri 账号调用 `POST /auth/login`。新安装须先按[首次设置](accounts.md#首次设置)完成管理员密码设置。
2. 同源网页使用登录得到的 HttpOnly Cookie；原生和外部客户端使用响应中的 `access_token` 作为 Bearer 凭据。权限由账号角色及服务器授权决定。
3. 调用 `GET /info`，检查需要的能力。浏览现有媒体库可从 `GET /works` 或 `GET /sources` 开始，不需要重新扫描。
4. 管理员用 `POST /sources` 添加本地媒体路径时，默认开启自动扫描、归档、获取作品资料和媒体信息。查询后台任务了解结果；仅在需要手动控制时调用对应任务接口。单独的扫描接口只更新文件索引，不直接生成作品。

[协议约定](protocol.md)说明认证、错误、分页和编辑时的修订检查。[OpenAPI](openapi.json)包含当前公开接口的请求与响应格式，可导入接口工具。示例中的身份、修订和游标都是不透明字符串，原样保存和传回。

## 按用途查阅

| 你要做什么 | 文档 |
| --- | --- |
| 登录、用户和会话 | [账号与会话](accounts.md) |
| 查询和编辑作品、搜索与语言 | [作品资料](catalog.md)、[人物/社团/标签](metadata-entities.md) |
| 添加媒体来源、浏览兼容来源、扫描文件 | [来源与扫描](scanning.md) |
| 调整作品根和文件归属 | [归档](organization.md)、[节点属性与规则](node-attributes.md)、[批量调整与差异](curation.md) |
| 设置自动处理或批量启动任务 | [自动处理策略](automation.md)、[批量处理任务](library-jobs.md) |
| 获取作品介绍和封面 | [作品资料获取](metadata-scraping.md)、[离线资料包](metadata-packages.md) |
| 读取音视频信息 | [媒体信息](media-probe.md)、[文件信息及图片](presentation.md) |
| 播放、字幕、队列与播放列表 | [播放接口](playback.md)、[进度及播放选择](listening.md)、[片段版本与字幕关系](media-associations.md) |
| 手工编辑或转换台本 | [台本编辑](subtitle-editing.md) |
| 偏好与收藏集 | [界面配套接口](presentation.md) |
| 连接通用 WebDAV 客户端 | [只读 WebDAV](webdav.md) |
| 调整网络、使用反向代理 | [网络接口](network.md)、[Nginx 使用教程](../USAGE.md#通过-nginx-访问-ruri) |
| 备份和换机 | [迁移与恢复](migration.md) |
| 查询或激活服务器授权 | [产品授权](licensing.md) |
| 排查任务问题、比较文件身份 | [日志](logs.md)、[实体比较](entities.md) |

## 独立服务契约

以下服务有独立地址和凭据，不随媒体库接口自动提供：

- [文字资料查询服务](scrape-service.md)及其 [OpenAPI](scrape-service.openapi.json)。普通 Ruri 用户无需另行部署此服务。
- [授权服务 OpenAPI](license-service.openapi.json)。使用运营方提供的服务地址；申请测试、邮件验证和管理等接口是否开放，由该服务提供者确认。不可用的接口不应显示为可执行功能。

公开契约包含当前可调用的手工台本接口，不表示支持自动识别、自动翻译等未列出的功能。可选组件缺失时按接口返回的不可用状态处理，不把接口名称或数据来源标记视为功能已开启。
