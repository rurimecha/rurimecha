# 文字生成与确认保存

以下 AI 路径相对 `/api/v1`，需管理员会话和有效服务器授权。授权状态见 `GET /license` 的 `capabilities.ai`。浏览器的写请求使用同源会话与 Origin/Referer。可通过 `GET /info` 的 `agent.text_gateway` 能力识别支持文字网关的 Core。机器可读定义见 [OpenAPI](openapi.json)。

默认 Ruri 构建可在内置文字 AI 网关和独立网关之间选择。两种模式分别保存用途配置；切换模式不会清除另一侧的模型选择。

## 连接和模型设置

- `GET /agent/settings` 读取当前启用状态、连接模式、模型用途配置和 ETag。`gateway_mode` 为 `local`（内置）或 `remote`（独立），`profiles` 表示当前模式，`profiles_by_mode` 保留两种模式各自的配置。`base_url` 和 `has_token` 表示已保存的独立网关连接；响应不返回令牌内容。
- `POST /agent/settings/test` 和 `GET /agent/models` 读取所选网关的模型。内置模式提交 `gateway_mode: local`；独立模式提交 `gateway_mode: remote`、HTTP 或 HTTPS 根地址 `base_url`，并可提交 `token`。省略令牌且地址与已保存值相同时沿用已保存令牌。读取列表不保存设置，也不发起生成请求。
- `PUT /agent/settings` 使用当前 ETag 作为 `If-Match` 保存当前模式的启用状态和完整 `profiles`。独立模式启用时必须提供地址和有效的已保存或新令牌。作品资料翻译、台本翻译及目录与文件名翻译可分别选择模型。新选的模型，以及从停用改为启用的用途所选模型，必须在当前目录可用；未安装的本地模型不能启用。仍保留的旧选择可随其他设置提交，便于更换或移除已从目录下线的模型。停用后可以继续手工编辑。

## 本地网关管理

`GET /ai-gateway/manage` 打开内置网关的 HTML 管理页。页面使用 Core 管理员会话，可增改、启用或停用供应商和模型。供应商密钥用于访问对应的模型服务。浏览器打开后收到未登录或需要管理员权限的响应时，可显示 HTML 提示。语言优先使用 `lang`，其次使用 `Accept-Language`。

`GET /ai-gateway/settings` 返回同一内置网关的配置与 ETag。`PUT /ai-gateway/settings` 用该 ETag 作为 `If-Match` 提交完整配置，成功后返回保存结果及新 ETag。`providers` 和 `models` 可用空数组，`timeout_seconds` 可为 1 至 3600，`max_concurrent` 可为 1 至 64。

`GET /ai-gateway/local-models` 返回网关设备上的固定本地模型目录和安装状态。当前目录包括 Hy-MT3 1.8B Q4_K_M。状态包含 `state`、`stage`、已下载与总字节数、`supported` 和可选错误码；只有 `installed` 的模型会进入可用模型列表。

- `POST /ai-gateway/local-models/{model}/install` 开始或继续下载和安装；`repair` 重新检查失败的下载，并在运行组件失败时重建固定组件；`cancel` 停止当前操作。操作异步执行并返回 `202`，随后轮询目录。
- `DELETE /ai-gateway/local-models/{model}` 卸载所选模型，不删除其他模型、供应商设置或共享的已验证下载。正在安装时返回 `409 model_busy`。
- 模型文件和固定运行组件发行包使用清单中的 HTTPS 地址、大小和 SHA-256 校验。运行组件固定为 llama.cpp `b12751`，不会解析最新版；网关会直接安装当前平台的官方发行包，无需编译工具。

- 供应商用唯一 `id` 标识，填写 `kind` 与 `enabled`，可指定 `base_url`。`openai_compatible` 必须填写服务地址。远端使用 HTTPS，本机回环地址可使用 HTTP。`key_env` 表示启动时配置的环境变量引用，管理页可以沿用该引用。若需要换用新的环境变量，请在服务器启动配置处理。
- 返回的 `has_key` 表示已配置可用密钥，缺省为 `false`。密钥用只写字段 `key` 提交。同一供应商类型和服务地址下，省略 `key` 保留密钥，空字符串清除密钥及其环境变量引用。更换带有密钥的供应商类型或服务地址时，需明确设置新密钥或清除旧密钥。
- 每个供应商独立选择 `proxy_mode`。`system` 使用网关宿主的代理设置，`direct` 不在应用内选择代理，`custom` 使用指定的 `proxy_url`。新供应商省略模式时使用 `system`，已有供应商省略模式时沿用该 ID 的设置。
- OpenAI、DeepSeek、OpenRouter 和 OpenAI 兼容供应商可用 `text_protocol` 设置默认文字请求方式，取值为 `chat_completions`（省略时使用）或 `responses`。服务地址为 API 根地址。其他类型使用各自的固定请求方式。
- `proxy_url` 适用于 `custom`，支持 HTTP、HTTPS、SOCKS5、SOCKS5H 及可选的 URL 用户名和密码。管理员读取和保存响应提供完整地址，响应禁止缓存。已有供应商继续使用 `custom` 时，省略 `proxy_url` 保留保存的地址，提供新地址则完整替换。保存其他模式会清除原自定义地址。兼容字段 `proxy_address` 只含协议、主机和端口，`has_proxy_credentials` 表示地址带有凭据。
- 模型使用所选供应商的 `provider` ID、供应商的 `model` ID、`json_mode`（`json_schema` 或 `json_object`）及 `enabled`。可选的 `alias` 留空时按模型名生成，重名时附加数字后缀。显式填写的别名须唯一，可使用 1 至 256 个非空格可见 ASCII 字符。响应返回保存后的别名，供应商和模型都已启用时才可在本地 AI 功能中选择。
- 模型可用 `text_protocol` 覆盖供应商设置，省略或传空字符串时继承。`json_mode` 独立选择模型支持的输出格式。

保存成功后，新请求使用保存的配置。缺少当前修订返回 `428`，修订过期返回 `412`，请重新读取配置。保存或验证失败时原配置保留。内置网关不可用时返回 `503`。

`POST /ai-gateway/provider-models` 接收当前供应商的 `id`、`kind`，可选 `base_url`、`key`、`proxy_mode` 和 `proxy_url`。省略模式或地址时按保存设置的规则沿用。供应商 ID、类型和有效服务地址都相同时，省略 `key` 可用已保存的密钥。空字符串表示本次查询不带密钥。返回 `{"models":["供应商模型 ID"]}`，可以为空，不保存此次输入。代理模式或地址不合法时返回 `400 provider_proxy_invalid`，供应商没有提供列表时返回 `422 provider_models_unsupported`，可手动填写模型 ID。

`POST /ai-gateway/test` 接收 `{"model":"已保存的模型别名"}` 或 `{"draft":{"provider":{...},"model":"供应商模型 ID","json_mode":"json_schema","text_protocol":"responses"}}`。两种方式任选其一，向供应商发送一次合成的结构化请求。草稿的 `provider` 与获取模型列表使用同一凭据沿用规则，可另附 `text_protocol`。草稿以模型覆盖优先，未覆盖时使用草稿提供的供应商默认值。测试可能消耗供应商额度。草稿测试不保存设置或调用记录。成功返回 `{"ok":true,"model":"测试的模型"}`，`model` 为已保存的别名或草稿中的供应商模型 ID。

测试错误通过 `error.code` 区分。`responses_required` 表示已有证据表明应使用 Responses，`protocol_unsupported` 仅确认当前方式不适用。`upstream_model_unavailable` 应检查服务地址、模型 ID 和访问权限，`upstream_auth_failed` 表示供应商拒绝了密钥，与返回 401 的管理会话错误不同。`output_format_unsupported` 表示应调整输出格式。限流、超时、拒答、截断、未完成以及无效输出分别使用 `upstream_rate_limited`、`upstream_timeout`、`model_refused`、`model_output_truncated`、`upstream_response_incomplete` 和 `invalid_model_output`。测试和正式请求都只按所选方式发送。

`GET /ai-gateway/activity` 需要 Ruri 管理员会话及有效服务器 AI 授权。可传 `from` 和 `to`（UTC 日期，`YYYY-MM-DD`，含当天），分别省略时无该端范围限制。返回 `models`（所选日期的累计用量）、`calls`（该范围内保留的明细）、`retention_limit`、`max_retention_limit` 和 `save_failed`。默认返回至多 50 条最新明细，返回 `next_offset` 时可传该值请求下一页。每条明细包含 RFC3339 时间、模型别名、耗时、结果和供应商提供的用量。通过已保存别名发起的测试计入用量。用量缺失时省略 `usage`。累计用量包含升级时仍保有的旧记录和之后的新调用。

`PUT /ai-gateway/activity/retention` 需要同一管理员权限，传入 `limit` 和上次读取的 `expected_limit`。设置范围为 0–10,000，默认为 1,000。减少上限立即删除超出的旧明细，设 0 时仅保留累计用量。成功返回 204。上限已被其他客户端更改时返回 `409 retention_changed`，应刷新后再操作。

## 翻译术语表

`GET /agent/translation-glossary` 读取当前资料库的术语表并返回 ETag。管理员可用 `PUT /agent/translation-glossary` 携带 `If-Match` 保存完整的 `entries`。条目包含 `terms`（从 `ja`、`zh-Hans`、`zh-Hant`、`en` 中至少选择两种）、可选 `context` 和 `disabled`。支持至多 256 条，每个术语至多 192 个 UTF‑8 字节，使用说明至多 512 字节。未保存过时返回内置表，保存空表后返回空表。保存不调用模型，修订冲突返回 `412`。读写需要管理员权限及有效 AI 授权。

作品资料翻译和名称翻译的创建请求可传 `use_glossary: true`，省略时默认关闭。选中时按本批原文和语言配对选择术语，只发送命中条目的原词、目标译法和使用说明。已有的系统提示词选择独立生效。当前文字网关的两个翻译配置可用 `translation_formats` 按模型别名指定 `hy_mt_structured`，未指定时使用通用格式。模型返回的段落必须覆盖本批文本并保留支持的占位符。

## 作品资料翻译

`GET /agent/metadata-translation-prompt` 返回 `{"prompt":"默认翻译提示词"}`，不调用模型。`POST /agent/jobs` 使用 `work_ids`、`target_locale` 及可选 `source_locale`、`fields`（`title`、`description`）、`expected_revisions`、`prompt`。每项任务可指定一个目标语言，支持简体中文、繁体中文、日语和英语。省略源语言时使用每部作品的原始语言。结果为 `202` 与任务 `Location`。

省略 `prompt` 或传空字符串时，不发送系统提示词。要使用中文默认提示词，先读出内容，再随创建请求提交。可传至多 4,000 个 Unicode 码点的本次提示词，非空值若全文为空白或包含空字符则返回 `400`。固定的翻译要求、资料范围和输出校验仍适用。可编辑提示词仅随本次任务发送，不写入任务历史或 AI 设置。

`GET /agent/jobs` 返回近期任务。`GET /agent/jobs/{job}` 返回逐部作品状态、生成的 `candidate_ids`、可选 `description_parts` 和 `work_revision`。选定简介时，详细介绍的译文保留图片地址、顺序和曲目时长。`POST /agent/jobs/{job}/cancel` 请求停止生成。生成文字是待选内容，已发布的资料保持原值。

读取候选和最新的 [元数据编辑快照](metadata-scraping.md)，核对项目 `work_revision`。用户确认后，在同一次 `PATCH /works/{work}/metadata-editor` 用快照的 `If-Match` 提交选中的目标语言字段，可分别选择标题、简介和详细介绍。变更后的作品需重新核对，过期的确认返回 `412`。

从元数据编辑器发起翻译时，`GET /works/{work}/metadata-editor` 的 `translation_fields` 列出含文字的可选字段。创建任务传入其中属于 `source_locale` 的 `editor_fields`，与旧的 `fields` 互斥。提供方的年龄分类和原始介绍数据不在可选范围，详细介绍使用 `texts/{locale}/description_parts`。`skip_existing` 默认为 true，会跳过目标语言已有文字的整个字段。设置为 false 可重新生成。

对应任务项的 `editor_values` 按目标语言字段返回 `value` 和可核对的文本位置 `paths`。确认时提交 `PATCH /works/{work}/metadata-editor`，传 `translation_job` 与 `translated_fields`，后者按目标字段列出与 `paths` 顺序一致的核对文字。沿用 `If-Match`，与手动 `values`、`unlock` 互斥。确认会保存并保护所选目标字段，文本以外的数据按草稿保留。

`POST /agent/jobs` 可为一个作品的 `editor_fields` 任务传 `review_required: true`。同一作品已有此类进行中或待确认的翻译时，创建请求返回 `409 translation_review_pending`。此时调用 `GET /works/{work}/translation-job` 继续查看，响应为 `{"job":{…}}`，无此类任务时为 `{"job":null}`。此查询和任务列表沿用 AI 任务的授权规则。

此类任务在运行时的 `review_state` 为 `waiting`，已生成的结果为 `pending`。待确认结果保留至确认、放弃或作品被移除。确认沿用元数据编辑接口，在同一事务中写入选中的目标字段并把结果标记为 `applied`。`POST /agent/jobs/{job}/discard` 可将待确认结果标记为 `discarded`，该操作不改动作品。以已处理结果再次确认会收到 `409 translation_review_closed`。任务中断或失败时没有待确认结果，可重新创建任务。

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
