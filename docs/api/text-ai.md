# 文字生成与确认保存

以下 AI 路径相对 `/api/v1`，需管理员会话和有效服务器授权。授权状态见 `GET /license` 的 `capabilities.ai`。浏览器的写请求使用同源会话与 Origin/Referer。可通过 `GET /info` 的 `agent.text_gateway` 能力识别支持文字网关的 Core。机器可读定义见 [OpenAPI](openapi.json)。

## 连接和模型设置

- `GET /agent/settings` 读取当前配置和 ETag。`gateway_mode` 为 `local` 或 `remote`，旧配置按 `remote` 读取。`profiles` 对应已选择模式，`profiles_by_mode` 返回分别保留的 `local` 和 `remote` 用途配置。`base_url` 与 `has_token` 始终表示已保存的远程连接，推理令牌只在提交时使用。
- `POST /agent/settings/test` 读取指定模式的可选模型别名，返回 `valid` 与 `models`。选择 `local` 时无需提供地址或推理令牌。选择 `remote` 时使用填写的 Gateway 根地址及令牌，地址相同时可省略令牌并沿用已保存的值。测试列表不调用模型生成，配置保持原值。`GET /agent/models` 读取已保存模式的模型列表。
- `PUT /agent/settings` 使用当前 ETag 作为 `If-Match` 保存启用状态、`gateway_mode` 和该模式的完整 `profiles`。省略 `gateway_mode` 按 `remote` 处理。保存 `local` 时保留远程地址、令牌和用途配置，保存 `remote` 时保留本地用途配置。远程地址变化时需提供对应的令牌。同地址省略 `token` 可保留原令牌，空字符串清除令牌。作品资料翻译、台本翻译、目录与文件名翻译、归档建议可分别选择模型。停用后可以继续手工编辑。

## 本地网关管理

`GET /ai-gateway/manage` 打开内置网关的 HTML 管理页。页面使用 Core 管理员会话，可增改、启用或停用供应商和模型。这里管理的供应商密钥用于访问上游模型服务，远程 Gateway 推理令牌在 AI 设置中另行配置。

`GET /ai-gateway/settings` 返回同一内置网关的配置与 ETag。`PUT /ai-gateway/settings` 用该 ETag 作为 `If-Match` 提交完整配置，成功后返回保存结果及新 ETag。`providers` 和 `models` 可用空数组，`timeout_seconds` 可为 1 至 3600，`max_concurrent` 可为 1 至 64。

- 供应商用唯一 `id` 标识，填写 `kind` 与 `enabled`，可指定 `base_url`。`openai_compatible` 必须填写服务地址。远端使用 HTTPS，本机回环地址可使用 HTTP。`key_env` 表示启动时配置的环境变量引用，管理页可以沿用该引用。若需要换用新的环境变量，请在服务器启动配置处理。
- 返回的 `has_key` 表示已配置可用密钥，缺省为 `false`。密钥用只写字段 `key` 提交。同一供应商类型和服务地址下，省略 `key` 保留密钥，空字符串清除密钥及其环境变量引用。更换带有密钥的供应商类型或服务地址时，需明确设置新密钥或清除旧密钥。
- 模型使用唯一 `alias`、所选供应商的 `provider` ID、供应商的 `model` ID、`json_mode`（`json_schema` 或 `json_object`）及 `enabled`。供应商和模型都已启用时，该别名才可用于本地 AI 功能。

保存成功后，新请求使用保存的配置。缺少当前修订返回 `428`，修订过期返回 `412`，请重新读取配置。保存或验证失败时原配置保留。内置网关不可用时返回 `503`。

`POST /ai-gateway/provider-models` 接收当前供应商的 `id`、`kind`、可选 `base_url` 和可选 `key`。供应商 ID、类型和有效服务地址都相同时，省略 `key` 可用已保存的密钥。空字符串表示本次查询不带密钥。返回 `{"models":["供应商模型 ID"]}`，可以为空，不保存供应商或模型设置。供应商没有提供列表时返回 `422 provider_models_unsupported`，可手动填写模型 ID。

`POST /ai-gateway/test` 接收 `{"model":"已保存的模型别名"}`，向已启用模型发送一次合成的结构化请求。成功返回 `{"ok":true,"model":"已保存的模型别名"}`。该测试可能消耗供应商额度，配置保持原值。

## 作品资料翻译

`POST /agent/jobs` 使用 `work_ids`、`target_locale` 及可选 `source_locale`、`fields`（`title`、`description`）、`expected_revisions`。每项任务可指定一个目标语言，支持简体中文、繁体中文、日语和英语。省略源语言时使用每部作品的原始语言。结果为 `202` 与任务 `Location`。

`GET /agent/jobs` 返回近期任务。`GET /agent/jobs/{job}` 返回逐部作品状态、生成的 `candidate_ids`、可选 `description_parts` 和 `work_revision`。选定简介时，详细介绍的译文保留图片地址、顺序和曲目时长。`POST /agent/jobs/{job}/cancel` 请求停止生成。生成文字是待选内容，已发布的资料保持原值。

读取候选和最新的 [元数据编辑快照](metadata-scraping.md)，核对项目 `work_revision`。用户确认后，在同一次 `PATCH /works/{work}/metadata-editor` 用快照的 `If-Match` 提交选中的目标语言字段，可分别选择标题、简介和详细介绍。变更后的作品需重新核对，过期的确认返回 `412`。

## 台本翻译

`POST /nodes/{node}/transcript-drafts` 接受现有轨道 `track` 和读取编辑器时取得的 `revision`，返回独立草稿、`201`、`Location` 和草稿 ETag。保留原文与现有时间，轨道修订变化返回 `412`。已有草稿可直接翻译。

`POST /transcript-drafts/{draft}/translations` 使用草稿 `If-Match` 和目标语言 `target_language`，可选 `cue_ids`、`replace`、`request_key`。默认处理未锁定、尚无目标译文的内容。响应 `202`、提案和 `Location`。通过 `GET /transcript-drafts/{draft}/proposals` 查询历史，使用 `GET /transcript-proposals/{proposal}` 轮询提案。可发出 `POST .../cancel` 请求停止，或 `POST .../resume` 在草稿和模型设置仍匹配时继续剩余批次。

用户核对后调用 `POST /transcript-proposals/{proposal}/apply`，提交目标草稿 `If-Match`。原文、时间和其他语言保留。采用后的稿件可按 [台本编辑和保存](subtitle-editing.md) 导出或另存译文、双语版本。

## 目录和作品名称批量翻译

`GET /info` 发布 `agent.node_name_jobs` 能力。管理员可用 [名称接口](scanning.md) 分语言编辑或生成单项译名，也可创建一个后台任务批量保存。

- `POST /agent/node-name-jobs/preview` 与 `POST /agent/node-name-jobs` 都接受 `{"directory_id":"节点 ID","target_locale":"en","skip_existing":true}`。`directory_id` 与 `work_id` 任选其一。支持 `zh-Hans`、`zh-Hant`、`ja` 和 `en`。省略 `skip_existing` 时为 true，仅计算所选语言的已有译名。
- 目录范围为选中目录和已索引的子树，包括可用文件和目录。媒体来源的显示名称独立于目录节点。作品范围为当前归属该作品的可用文件和目录，分组、类型筛选和分页不影响范围。独立子作品使用自己的范围。预览返回 `total`、`existing` 和 `eligible`，无需调用模型。
- 创建返回 `202`、任务对象与 `Location`。使用 `GET /agent/node-name-jobs` 读取近期任务，`GET /agent/node-name-jobs/{job}` 读取一项，`POST /agent/node-name-jobs/{job}/cancel` 请求取消。任务包含 `input`、`state`、`total`、`processed`、`translated`、`skipped`、`conflicted`、`failed`，以及时间和可选 `error_code`。同一库已有进行中的任务时返回 `409 agent_busy`。
- 每批发送目录名称和文件名主体。结果校验成功后仅保存指定语言，保存时复核节点范围、原名和别名修订。`skip_existing=false` 允许替换任务开始时已有的目标译名。任务进行中的手工修改保留并计入冲突。已保存的译名在任务失败、取消或中断后保留。任务结束的状态包括 `succeeded`、`partial`、`failed`、`canceled` 和 `interrupted`。

## 归档建议

对已完成完整扫描且归档输入就绪的来源，`POST /sources/{source}/archive-suggestions` 接受目录 `scope_node_id` 与可选 `instruction`。响应包含最多 12 项候选编辑、完整影响计数和最多 50 条影响明细。影响说明包括节点归属、字幕和视角关联。候选仅包含指定作品、作品根边界、标为非独立作品、排除及恢复收录。模型只能引用当前范围中提供的可用候选。范围过大时，可从更小的目录生成。

预览保留已保存的归属和决定。非空候选附带 ETag。用户确认后，向 `POST /sources/{source}/archive-suggestions/apply` 提交原目录与完整 `edits` 数组，以及该预览的 `If-Match`。Core 重新检查范围、基准和实际影响，在同一事务中保存决定并更新归属。返回 `edit_count` 与 `summary`。过期返回 `412`，来源不完整、锁定或范围外影响返回 `409`。
