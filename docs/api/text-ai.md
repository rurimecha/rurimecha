# 文字生成与确认保存

以下 AI 路径相对 `/api/v1`，需管理员会话和有效服务器授权。授权状态见 `GET /license` 的 `capabilities.ai`。浏览器的写请求使用同源会话与 Origin/Referer。可通过 `GET /info` 的 `agent.text_gateway` 能力识别支持文字网关的 Core。机器可读定义见 [OpenAPI](openapi.json)。

默认 Ruri 构建通过内置文字 AI 网关管理和使用模型。

## 连接和模型设置

- `GET /agent/settings` 读取当前启用状态、模型用途配置和 ETag。`gateway_mode` 为 `local`，`profiles` 与 `profiles_by_mode.local` 均表示内置网关的用途配置。兼容字段 `base_url` 与 `has_token` 分别返回空字符串和 `false`。
- `POST /agent/settings/test` 与 `GET /agent/models` 读取内置网关中可用的模型别名。前者返回 `valid` 和 `models`，可提交空对象。读取模型列表无需调用生成服务，也不保存设置。
- `PUT /agent/settings` 使用当前 ETag 作为 `If-Match` 保存启用状态和完整的 `profiles`。`gateway_mode` 可省略，也可显式提交 `local`。测试或保存时传入其他模式、非空 `base_url` 或 `token` 值会返回 `400`。作品资料翻译、台本翻译、目录与文件名翻译、归档建议可分别选择模型。停用后可以继续手工编辑。

## 本地网关管理

`GET /ai-gateway/manage` 打开内置网关的 HTML 管理页。页面使用 Core 管理员会话，可增改、启用或停用供应商和模型。供应商密钥用于访问对应的模型服务。

`GET /ai-gateway/settings` 返回同一内置网关的配置与 ETag。`PUT /ai-gateway/settings` 用该 ETag 作为 `If-Match` 提交完整配置，成功后返回保存结果及新 ETag。`providers` 和 `models` 可用空数组，`timeout_seconds` 可为 1 至 3600，`max_concurrent` 可为 1 至 64。

- 供应商用唯一 `id` 标识，填写 `kind` 与 `enabled`，可指定 `base_url`。`openai_compatible` 必须填写服务地址。远端使用 HTTPS，本机回环地址可使用 HTTP。`key_env` 表示启动时配置的环境变量引用，管理页可以沿用该引用。若需要换用新的环境变量，请在服务器启动配置处理。
- 返回的 `has_key` 表示已配置可用密钥，缺省为 `false`。密钥用只写字段 `key` 提交。同一供应商类型和服务地址下，省略 `key` 保留密钥，空字符串清除密钥及其环境变量引用。更换带有密钥的供应商类型或服务地址时，需明确设置新密钥或清除旧密钥。
- 每个供应商独立选择 `proxy_mode`。`system` 使用网关宿主的代理设置，`direct` 不在应用内选择代理，`custom` 使用指定的 `proxy_url`。新供应商省略模式时使用 `system`，已有供应商省略模式时沿用该 ID 的设置。
- `proxy_url` 仅写，适用于 `custom`，支持 HTTP、HTTPS、SOCKS5、SOCKS5H 及可选的 URL 用户名和密码。已有供应商继续使用 `custom` 时，省略 `proxy_url` 保留保存的代理地址和凭据，提供新地址则完整替换。保存其他模式会清除原自定义地址。响应中的 `proxy_address` 只含协议、主机和端口，`has_proxy_credentials` 表示地址带有凭据。
- 模型使用唯一 `alias`、所选供应商的 `provider` ID、供应商的 `model` ID、`json_mode`（`json_schema` 或 `json_object`）及 `enabled`。供应商和模型都已启用时，该别名才可用于本地 AI 功能。

保存成功后，新请求使用保存的配置。缺少当前修订返回 `428`，修订过期返回 `412`，请重新读取配置。保存或验证失败时原配置保留。内置网关不可用时返回 `503`。

`POST /ai-gateway/provider-models` 接收当前供应商的 `id`、`kind`，可选 `base_url`、`key`、`proxy_mode` 和 `proxy_url`。省略模式或地址时按保存设置的规则沿用。供应商 ID、类型和有效服务地址都相同时，省略 `key` 可用已保存的密钥。空字符串表示本次查询不带密钥。返回 `{"models":["供应商模型 ID"]}`，可以为空，不保存此次输入。代理模式或地址不合法时返回 `400 provider_proxy_invalid`，供应商没有提供列表时返回 `422 provider_models_unsupported`，可手动填写模型 ID。

`POST /ai-gateway/test` 接收 `{"model":"已保存的模型别名"}`，按照已保存的供应商代理设置向已启用模型发送一次合成的结构化请求。成功返回 `{"ok":true,"model":"已保存的模型别名"}`。该测试可能消耗供应商额度，配置保持原值。

`GET /ai-gateway/activity` 需要 Ruri 管理员会话及有效服务器 AI 授权。可传 `from` 和 `to`（UTC 日期，`YYYY-MM-DD`，含当天），分别省略时无该端范围限制。返回 `models`（所选日期的累计用量）、`calls`（该范围内保留的明细）、`retention_limit`、`max_retention_limit` 和 `save_failed`。默认返回至多 50 条最新明细，返回 `next_offset` 时可传该值请求下一页。每条明细包含 RFC3339 时间、模型别名、耗时、结果和供应商提供的用量。测试模型也计入用量。用量缺失时省略 `usage`。累计用量包含升级时仍保有的旧记录和之后的新调用。

`PUT /ai-gateway/activity/retention` 需要同一管理员权限，传入 `limit` 和上次读取的 `expected_limit`。设置范围为 0–10,000，默认为 1,000。减少上限立即删除超出的旧明细，设 0 时仅保留累计用量。成功返回 204。上限已被其他客户端更改时返回 `409 retention_changed`，应刷新后再操作。

## 作品资料翻译

`GET /agent/metadata-translation-prompt` 返回 `{"prompt":"默认翻译提示词"}`，不调用模型。`POST /agent/jobs` 使用 `work_ids`、`target_locale` 及可选 `source_locale`、`fields`（`title`、`description`）、`expected_revisions`、`prompt`。每项任务可指定一个目标语言，支持简体中文、繁体中文、日语和英语。省略源语言时使用每部作品的原始语言。结果为 `202` 与任务 `Location`。

省略 `prompt` 或传空字符串时，不发送系统提示词。要使用中文默认提示词，先读出内容，再随创建请求提交。可传至多 4,000 个 Unicode 码点的本次提示词，非空值若全文为空白或包含空字符则返回 `400`。固定的翻译要求、资料范围和输出校验仍适用。可编辑提示词仅随本次任务发送，不写入任务历史或 AI 设置。

`GET /agent/jobs` 返回近期任务。`GET /agent/jobs/{job}` 返回逐部作品状态、生成的 `candidate_ids`、可选 `description_parts` 和 `work_revision`。选定简介时，详细介绍的译文保留图片地址、顺序和曲目时长。`POST /agent/jobs/{job}/cancel` 请求停止生成。生成文字是待选内容，已发布的资料保持原值。

读取候选和最新的 [元数据编辑快照](metadata-scraping.md)，核对项目 `work_revision`。用户确认后，在同一次 `PATCH /works/{work}/metadata-editor` 用快照的 `If-Match` 提交选中的目标语言字段，可分别选择标题、简介和详细介绍。变更后的作品需重新核对，过期的确认返回 `412`。

从元数据编辑器发起翻译时，`GET /works/{work}/metadata-editor` 的 `translation_fields` 列出含文字的可选字段。创建任务传入其中属于 `source_locale` 的 `editor_fields`，与旧的 `fields` 互斥。提供方的年龄分类和原始介绍数据不在可选范围，详细介绍使用 `texts/{locale}/description_parts`。`skip_existing` 默认为 true，会跳过目标语言已有文字的整个字段。设置为 false 可重新生成。

对应任务项的 `editor_values` 按目标语言字段返回 `value` 和可核对的文本位置 `paths`。确认时提交 `PATCH /works/{work}/metadata-editor`，传 `translation_job` 与 `translated_fields`，后者按目标字段列出与 `paths` 顺序一致的核对文字。沿用 `If-Match`，与手动 `values`、`unlock` 互斥。确认会保存并保护所选目标字段，文本以外的数据按草稿保留。

## 台本翻译

`POST /nodes/{node}/transcript-drafts` 接受现有轨道 `track` 和读取编辑器时取得的 `revision`，返回独立草稿、`201`、`Location` 和草稿 ETag。保留原文与现有时间，轨道修订变化返回 `412`。已有草稿可直接翻译。

`POST /transcript-drafts/{draft}/translations` 使用草稿 `If-Match` 和目标语言 `target_language`，可选 `cue_ids`、`replace`、`request_key`。默认处理未锁定、尚无目标译文的内容。响应 `202`、提案和 `Location`。通过 `GET /transcript-drafts/{draft}/proposals` 查询历史，使用 `GET /transcript-proposals/{proposal}` 轮询提案。可发出 `POST .../cancel` 请求停止，或 `POST .../resume` 在草稿和模型设置仍匹配时继续剩余批次。

用户核对后调用 `POST /transcript-proposals/{proposal}/apply`，提交目标草稿 `If-Match`。原文、时间和其他语言保留。采用后的稿件可按 [台本编辑和保存](subtitle-editing.md) 导出或另存译文、双语版本。

## 目录和作品名称批量翻译

`GET /info` 发布 `agent.node_name_jobs` 能力。管理员可用 [名称接口](scanning.md) 分语言编辑或生成单项译名，也可创建一个后台任务批量保存。

- `POST /agent/node-name-jobs/preview` 与 `POST /agent/node-name-jobs` 都接受 `{"directory_id":"节点 ID","target_locale":"en","skip_existing":true}`。`directory_id` 与 `work_id` 任选其一。支持 `zh-Hans`、`zh-Hant`、`ja` 和 `en`。省略 `skip_existing` 时为 true，仅计算所选语言的已有译名。
- 目录范围为选中目录和已索引的子树，包括可用文件和目录。媒体来源的显示名称独立于目录节点。作品范围为当前归属该作品的可用文件和目录，分组、类型筛选和分页不影响范围。独立子作品使用自己的范围。预览返回 `total`、`existing`、`eligible` 和目标语言的默认名称翻译提示词 `prompt`，无需调用模型。
- 创建可传 `prompt` 临时覆盖名称翻译提示词，最多 4,000 个字符，不得全为空白或包含空字符。省略或传空字符串时，Ruri 不发送系统提示词。要使用默认提示词，请将预览返回的 `prompt` 随创建请求提交。名称翻译提示词随该任务发给模型，不写入 AI 设置或任务历史。创建返回 `202`、任务对象与 `Location`。使用 `GET /agent/node-name-jobs` 读取近期任务，`GET /agent/node-name-jobs/{job}` 读取一项，`POST /agent/node-name-jobs/{job}/cancel` 请求取消。任务包含 `input`、`state`、`total`、`processed`、`translated`、`skipped`、`conflicted`、`failed`，以及时间和可选 `error_code`。同一库已有进行中的任务时返回 `409 agent_busy`。
- 每批发送目录名称和文件名主体。结果校验成功后仅保存指定语言，保存时复核节点范围、原名和别名修订。`skip_existing=false` 允许替换任务开始时已有的目标译名。任务进行中的手工修改保留并计入冲突。已保存的译名在任务失败、取消或中断后保留。任务结束的状态包括 `succeeded`、`partial`、`failed`、`canceled` 和 `interrupted`。

## 归档建议

对已完成完整扫描且归档输入就绪的来源，`POST /sources/{source}/archive-suggestions` 接受目录 `scope_node_id` 与可选 `instruction`。响应包含最多 12 项候选编辑、完整影响计数和最多 50 条影响明细。影响说明包括节点归属、字幕和视角关联。候选仅包含指定作品、作品根边界、标为非独立作品、排除及恢复收录。模型只能引用当前范围中提供的可用候选。范围过大时，可从更小的目录生成。

预览保留已保存的归属和决定。非空候选附带 ETag。用户确认后，向 `POST /sources/{source}/archive-suggestions/apply` 提交原目录与完整 `edits` 数组，以及该预览的 `If-Match`。Core 重新检查范围、基准和实际影响，在同一事务中保存决定并更新归属。返回 `edit_count` 与 `summary`。过期返回 `412`，来源不完整、锁定或范围外影响返回 `409`。
