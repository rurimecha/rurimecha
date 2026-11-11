# 迁移与恢复

全部操作要求管理员。Web 使用同源会话并遵守写请求的 Origin/Referer 校验。原生和外部客户端使用 Bearer。迁移包独立于 API 版本和数据库结构。

| 操作 | 请求 |
| --- | --- |
| `POST /migration/export` | JSON `{"kind":"works","work_id":""}`。kind 为 works、system 或 complete，分别代表作品资料、系统数据、两者一起。仅 works 可指定 work_id，省略或空字符串为全部作品。system 和 complete 要求 password（至少 12 个字符，最多 1024 字节）。成功返回 `application/octet-stream`，附件扩展名 .ruribackup |
| `POST /migration/upload` | 请求类型 `application/octet-stream`，正文是完整包。返回 `{"upload_id":"…"}`。同一实例只保留最近一次成功上传的包，限上传者使用，20 分钟后失效 |
| `POST /migration/inspect` | JSON `{"upload_id":"…","password":"…"}`，检查包并返回来源和范围 |
| `POST /migration/preview` | JSON `{"upload_id":"…","password":"…","paths":{"旧来源 ID":"/新设备/媒体"},"target_work":""}`。验证目标、引用和缺失文件，无持久修改 |
| `POST /migration/restore` | 同 preview。向现有单部作品写入时，还需提交预览给出的 target_revision。恢复再次校验，失败回滚。成功后同次上传不能再次使用 |

inspect、preview 和 restore 返回 `kind`、`single`、`sources`、`works`、`accounts`、`missing`、`missing_paths`、`restart_required`，单部覆盖预览还返回 `target_revision`。sources 项有字符串 id、name、path 和布尔 remote。remote 的 path 为已配置的下载目录，可为空。missing_paths 最多列出 20 个相对路径示例。导入和导出目前接受未压缩数据最多 256 MiB 的包。

- 作品包包含当前作品及媒体引用、刮削结果、提供方和时间、各语言的完成状态、人工覆盖、图片、应用内字幕和台本草稿。完整缓存仍按正常规则使用，过期或缺项仍按原设置处理。同编号作品共享已有成功结果。已有目标商品缓存优先保留，恢复作品自身的资料与保护仍按包应用。
- 系统包包含全部账号及密码校验数据、Ruri 保存的服务凭据、全局设置、媒体来源、所有账号的偏好、收藏集、播放列表、收听进度和珍藏片段。恢复系统数据会撤销现有会话，请重启服务并使用迁移前的账号登录。只恢复系统时，作品与媒体引用仍保留，可随后恢复作品资料。现有目标若包含其他账号或个人记录，拒绝系统恢复。
- 迁移不携带媒体原文件、日志、任务历史、旧编辑版本、会话、设备授权和宿主机设置。目标挂载和监听地址由管理员配置。
- 本地来源需指定绝对目录，已登记同一来源时可省略并沿用它的目录。按来源和相对路径恢复，缺失仍可导入。完整包可以复用目标库按同一根目录自动归档、且未人工修改的作品。其他冲突返回 409，可改用单部包并显式指定目标。单部覆盖要求同一作品相对路径，保留目标作品 ID。
- 版本不支持或包格式错误返回 400 migration_archive。密码错误返回 400 migration_password。目标冲突或预览后的作品已修改返回 409 migration_conflict。有相关后台任务时返回 409 migration_busy。上传过期返回 404 migration_upload_expired。系统恢复后重启前的写请求返回 409 migration_restart_required。包过大返回 413 migration_size。
