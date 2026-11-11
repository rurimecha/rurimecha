# 作品与元数据

当前契约采用节点唯一有效归属，目录边界和文件转移见 [归纳接口](organization.md)。

所有路径相对 `/api/v1`，详细字段类型见 openapi.json。作品及公开资源读取允许普通已登录用户；修改共享资料、候选管理和维护操作需要管理员。写操作的修订、完整替换和 force 规则见 protocol.md。

## 接口

| 方法 | 路径 | 行为 |
| --- | --- | --- |
| GET / POST | /works | 作品分页/创建 |
| GET / PUT / DELETE | /works/{work} | 作品详情/完整替换/删除 |
| POST | /works/{work}/release | 解除全部人工归纳，保留元数据，详见 organization.md |
| GET | /works/{work}/localized?locale=en-US | 按字段读取回退后的显示文本 |
| GET / POST | /works/{work}/texts | 文本分页/增加语言 |
| GET / PUT / DELETE | /works/{work}/texts/{locale} | 指定语言精确读取/替换/删除 |
| GET / POST | /works/{work}/members | 成员分页/加入或排除决定 |
| GET / PUT / DELETE | /works/{work}/members/{member} | 成员详情/替换/删除 |
| GET / POST | /works/{work}/groups | 播放组分页/创建 |
| GET / PUT / DELETE | /works/{work}/groups/{group} | 播放组详情/替换/删除 |
| GET / POST | /works/{work}/candidates | 候选分页/创建 |
| GET / PUT / DELETE | /works/{work}/candidates/{candidate} | 候选详情/替换/删除 |
| POST | /search-index/rebuild | 同步、原子的搜索索引维护，无请求体，成功 204 |

GET /works 支持 q、identification_state、node_id、include_inactive 及分页。默认只返回 active=true 的作品；include_inactive=true 可查保留的元数据记录。node_id 仅匹配 Included 成员；可组合过滤。成员列表支持 state、node_id、playback_group_id、media_only（默认 false；true 在分页前仅保留 media_kind=audio/video）；候选列表支持 field、locale、decision、origin；其他子列表只支持分页。子记录必须属于 URL 指定作品，跨作品读取或修改返回 404。

## 作品

创建示例：

```http
POST /api/v1/works
Authorization: Bearer <token>
Content-Type: application/json

{"original_locale":"ja","texts":[{"locale":"ja","title":"雨の夜","description":"原文紹介"},{"locale":"zh-Hans","title":"雨夜"},{"locale":"zh-Hant","title":"雨夜繁體"},{"locale":"en","title":"Rainy Night"}],"languages":["ja"],"identifiers":[{"namespace":"dlsite","value":"RJ01234567"}]}
```

响应包含 id、revision、created_at、updated_at、只读 root_node_id（无根时为空字符串）、active，以及以下全部可编辑字段：

| 字段 | 含义与默认值 |
| --- | --- |
| original_locale | 原文语言，缺省 und；与音视频语言独立 |
| texts | 每种 locale 一份 title / description / description_parts，缺省空集合 |
| release_date | 发行日期，缺省空字符串 |
| release_date_precision | 空、year、month、day；分别匹配空、2026、2026-10、2026-10-29 |
| languages | 音视频正文语言标识集合，不混入界面或字幕语言 |
| identifiers | namespace / value 集合；同外部编号允许存在不同作品 |
| credits | role / name / sort_order 集合；角色采用稳定代码如 voice_actor |
| tags | category / name 集合 |
| cover_ref | null 或 kind / locator 对象；保留来源封面引用，本地图片读取见 metadata-scraping.md |
| product_info | 商品扩展字段；规范样图、年龄分级、语言版本及编辑保护见 metadata-scraping.md |
| identification_state | pending（默认）、identified、conflicted |
| default_playback_group_id | 未选为空；必须先创建同作品组再设置；PUT 省略/null 保留，显式不同值保存默认组覆盖，不冻结成员 |

空作品合法，可先创建描述草稿再指定目录根。PUT 完整替换描述元数据，没有提交的语言会被删除；默认组省略/null 保留。描述、分组和视角编辑均不改变目录边界；详细生命周期见 [归纳接口](organization.md)。

## 三语文本与回退

语言标识采用 BCP 47。作品资料支持 zh-Hans、zh-Hant、ja、en 和其他具体语言，未知资料使用 und。未区分简繁的 zh 不作为作品资料语言。音视频正文语言独立。original_locale 对应原始资料，允许暂时没有对应文本行；翻译独立保存。

POST /texts 的请求为 `{"locale":"en","title":"Rainy Night","description":"English introduction"}`。PUT /texts/en 的请求包含 title / description / description_parts，不包含 locale；省略某字段会清空该字段。重复创建同一规范化语言返回 409；不存在的精确读取、替换、删除返回 404。允许删除原文行或最后一份文本，不自动改写 original_locale。

GET /localized?locale=en-US 返回：

```json
{"work_id":"opaque-work-id","revision":"7","title":"Rainy Night","title_locale":"en","description":"","description_locale":"","description_parts":null,"description_parts_locale":""}
```

`GET /works/{work}/localized` 返回生效语言的标题和简介。可用 `locale` 请求确切的已保存语言，缺失时返回 404。所选语言缺少的字段留空。

`description_parts` 是按顺序保存的结构化介绍，纯文本简介 `description` 继续保留。段落包含 `kind`（`text`、`image`、`tracks`）、可选 `heading` / `text` / `images` / `tracks`。图片包含 `url` 和可选 `thumbnail_url,width,height,caption`；曲目包含 `title` 和可选 `duration_text`，时长保留商品原文。图片地址只接受不含凭据的 HTTP(S) URL。全部文本都是纯文本，客户端不能作为 HTML 执行。

每种语言最多 256 段，段落 JSON 总计最多 256 KiB；每段最多 256 张图片、512 条商品曲目。完整替换时，省略/null/空数组清空结构化介绍；编辑其它字段并希望保留时须带回原值。人工保护使用 `manual:texts/<locale>/description_parts`，与同语言纯文本简介及其它语言分别保护/重置；删除语言会保护其整体缺失。已有 `description` 候选锁定同时保护该语言结构化介绍。`localized` 将结构化内容与纯文本简介作为同一语言单元回退；缺少的那部分保持空，不从其他语言补齐。两者非空时来源语言相同，`description_parts_locale` 在无段落时为空。空集合可以返回 null 或 []。

商品曲目仅用于介绍，不代表已下载文件、播放顺序或实际时长。编辑它不会创建成员、播放组或片段关系。样图、年龄分级、语言版本等商品字段见 [元数据刮削](metadata-scraping.md)。

## 成员与播放组

成员输入包括 node_id、state（默认 included）、origin、source_ref、locked、playback_group_id、play_order、media_kind、media_format。节点身份不可更换。有效 included 归属全局唯一；excluded 行是历史/排除记录，不是另一份归属。GET /works?node_id=... 查询当前所有者，结果最多一部。成员响应中的 alias 是节点共享别名，未设置为空字符串；别名通过节点接口维护，不属于成员编辑正文。

已登记目录必须通过目录决定改变归属，不能用成员 POST/DELETE 或 state 修改替代。已登记文件的 POST 保存显式 assign；有效成员改为 excluded 或 DELETE 保存文件 exclude；删除本来已 excluded 的历史记录只清理历史，不改当前所有者。其他作品已拥有该文件时不能通过成员 CRUD 保存覆盖，使用决定接口明确操作。跨作品转移使用 assign 决定和归纳应用，直接插入第二条 included 会 409。未登记的不透明节点可用于资料草稿，但不会影响目录归纳。存在祖先排除或跨来源目标时冲突。

播放组、顺序、锁定和媒体提示可编辑，文件及目录有效归属不随这些注解变化。显式成员编辑保留其播放设置；修改锁定成员需 force。成员被候选引用时不能删除，使用 excluded 或先处理证据；有视角绑定的直接排除/移组须先解绑。目录归纳导致的转移则自动保留绑定历史并原子处理。

组名在作品内唯一，kind 默认 custom，可 main/bonus/video/other；variant 默认 default，可 se/no_se。可播放成员最多一组；组和默认组必须同作品。仍被成员或默认组引用时不能删除。分组规则不跨子作品传播；MP3/WAV 格式名单无需一致。目录/字幕/图片不自动进入播放组。播放与字幕接口见 playback.md。

## 候选与人工决定

```json
{"field":"title","locale":"zh-Hans","value":"雨夜","origin":"manual","evidence_member_ids":["opaque-member-id"],"source_ref":"manual-editor","decision":"accepted","locked":true}
```

field、value 必填。文本字段 title / description 必须明确 locale；其余字段 locale 必须为空。origin 可为 rule、ai、manual（默认）、directory_name、file_tags、provider。decision 为 proposed（默认）、accepted、rejected。证据使用**成员关系 ID**，必须属于当前作品；重复值去重，排除成员可保留为历史证据。

| field | value |
| --- | --- |
| title / description | JSON 字符串 |
| release_date | `{"date":"2026-10","precision":"month"}` |
| languages | `["ja"]` |
| identifiers | `[{"namespace":"dlsite","value":"RJ01234567"}]` |
| credits | `[{"role":"voice_actor","name":"声优","sort_order":0}]` |
| tags | `[{"category":"场景","name":"雨夜"}]` |
| cover_ref | `{"kind":"node","locator":"opaque-node-id"}` |
| 任意上述字段明确清空 | null，仍须遵守相应 locale 规则 |

同作品、同标量字段、同语言最多一条 accepted；不同语言可独立采用、拒绝和锁定。多值字段允许多个 accepted，最终结果以作品资料接口返回为准。

候选 CRUD **只记录决定，不自动修改最终作品值**。前端显式调用文本或作品 PUT 保存最终文本；两次请求不是整体原子编辑。锁定保护对应候选/成员记录，单独编辑最终文本不会反向改写候选。修改或删除锁定记录、删除包含锁定记录的作品须 `force=true`。force 不允许删除仍被引用的证据或跳过过期修订。

## 搜索

q 搜索所有最终语言的标题、简介、结构化介绍的标题/正文/商品曲目名/图片说明，以及外部编号/命名空间、署名名称、标签名称、系列名和别名；候选即使 accepted，尚未写入最终值就不参与搜索。搜索与显示语言独立。

q 按空白分隔为最多 16 个字面词，每词必须命中已采用元数据或明确实体别名，可跨字段/语言匹配；不解释 OR/NOT、星号、百分号、下划线等查询语法。sort=relevance 要求非空 q。单独输入 RJ/BJ/VJ 或 D_ 开头的部分编号时，精确编号优先，其次编号前缀，再按标题、署名和其他命中排序。同级按创建时间倒序与 ID 排序。索引规范化等价匹配全角英文、半角片假名及 Unicode 大小写；不改写原文。简繁汉字、平/片假名、罗马音和翻译不自动等价。

`search_scope` 默认 `all`。设为 `title` 后，只在各语种的作品标题中查找，不使用介绍、编号、标签、署名或实体别名。相关度排序优先完整标题匹配，其次标题开头匹配、标题中匹配。没有关键词时范围不限制结果。至少三个 Unicode 字符使用全文索引，一两个字符也支持。

`GET /works/search-suggestions?q=RJ12&library_type=doujin` 返回至多五个本地作品候选，要求至少两个 Unicode 字符。可选 `search_scope=all|title` 与 `library_type=doujin|asmr`。返回 `{items:[{id,title,code,source_name,path}]}`，各字段均为字符串，缺少相应信息时为空。path 相对媒体来源，按作品返回，同编号副本不合并。候选按相关度排序，无分页。

索引在最终元数据修改及删除时同步。POST /search-index/rebuild 是维护操作，处理全库且可能较慢，不修改作品修订；请求取消或失败回滚。前端不应在每次查询前调用它。

片段/版本/文件绑定及字幕关联接口见 [media-associations.md](media-associations.md)。逻辑片段关系不自动形成，字幕配对在归纳应用时维护；描述性字幕注解不固定作品成员。

批量纠正、播放分类覆盖重置及旧记录清理见 [curation.md](curation.md)。

RJ商品事实共享缓存与人工字段保护见 [元数据刮削](metadata-scraping.md)。人工字段 GET 使用 `manual:texts/<locale>/title`、`manual:product_info/<key>` 等标记，Reset需传完整标记；正文值不带此前缀。默认任务只复用成功RJ缓存，显式force才联网刷新。

作品列表和详情在默认读请求中只返回一份生效资料。优先使用该账户为作品保存的语言，其次按 `metadata_locale_order` 选择已保存的语言。再依次回退到原文、`und` 和按代码排序的首个可用语言。同一作品的标题、简介、署名、标签、发行日期、封面和商品资料使用这一份。响应的 `metadata_locale` 标明生效语言，`available_metadata_locales` 提供切换选项。

`GET /works/{work}?metadata_locale=ja` 按指定语种读取整份展示资料。作品列表、分面和标签列表也接受 `metadata_locale`，没有该语种的作品不参与结果及统计。管理员使用 `GET /works/{work}/metadata` 读取完整对象以执行整对象替换。语言文本和编辑接口按其明确作用范围读取和修改数据。

可用 `work_ids` 限定要读取的作品 ID，以逗号分隔。标签和署名筛选、标签/分类统计及标题排序均使用生效资料。不同标签名分别计数。全文关键词仍匹配所有已保存语种，命中的作品按当前展示语种返回。音视频正文语言与上述设置独立。

## 商品家族与本地版本

`GET /works?group_by=family` 返回相同 `WorkPage` 结构；每项仍是一个真实本地作品，附带 `family:{key,version_count,local_work_count,conflicted}`。`group_by` 缺省为 `work`，可选 `work/family`。family 模式只浏览有效作品，与 include_inactive=true 同用返回 400；保留记录仍可用普通 work 模式查询。普通筛选筛选本地作品，随后按上游明确原作／语言版本关系归组、计数和分页，不在单页合并。同号目录副本均保留。q 还匹配家族关联缓存中的标题、简介、署名和标签；仅标题范围也适用于关联版本。relevance 将本地匹配排在仅关联缓存命中的家族前。存在匹配编号前缀的本地版本时，代表作品优先从这些版本中选择。仅标题搜索有本地命中时，从标题命中的作品中选择代表。无有效单一编号的作品单独显示，version_count=0 且详情 versions 为空；矛盾原作声明或过大的关联范围按单商品隔离并标记 conflicted。

`GET /works/{work}/family?limit=50&offset=0` 读取一个有效本地作品的当前家族。返回摘要字段、`anchor_work_id`、`versions` 和本地收录页 `local_works,total,limit,offset`。每个 version 含 original（有明确且无冲突的原作声明）、code、edition_language（未知为空）、source_url、cache_revision、metadata（PackageMetadata，尚未获取为 null）、local_work_count。最多 256 个关联商品；没有本地文件的商品只作为版本资料存在，不创建本地作品。

local_works 是 WorkDisplay 加 metadata（完整已保存资料，含人工修改）、root_path（服务端作品根目录完整路径，无目录为空，非 UTF-8 为 null）、source_name、playable。playable 只表示当前存在已纳入、来源启用且可用的音视频，不承诺所有编码可解码。锚点首先返回，其他收录按创建时间和 ID 排序；分页不遗漏或合并同编号的独立收录。所有字段在同一读快照中读取；不同请求间关系可能变化，客户端需刷新，不将 family.key 当作持久作品 ID。此接口不接受 metadata_locale 筛选；收录名称按账号偏好显示，完整资料通过 metadata 提供，界面切换资料语言不改变成员。

两个接口均需登录，普通用户可读。不存在／已移除／非有效的锚点返回 404；非法分页或 group_by 返回 400。家族没有写接口，读取不联网。播放、编辑、收藏、列表和进度仍使用选定的本地 work ID；价格、评分、销量和发行日期只属于各自商品，资料语言切换不能混用其他商品统计。

版本归组依据共享商品缓存中的上游关联观察。单独编辑某个本地作品的 language_versions／original_workno 描述只改变该作品资料，不更改全库的归组关系；手工纠正全局关系不属于当前接口。

## 默认商品版本与直接进入

`group_by=family` 的代表作品按当前账号的商品版本偏好选择。先限定符合查询筛选的候选，再按可播放、账号指定副本、版本语言顺序（version_mode=language）、明确原作、创建时间和 ID 选择稳定代表；资料语言与音视频格式偏好不代替商品版本语言。列表排序仍依据筛选结果中该家族首次出现的位置，归组计数及分页不因代表替换变化。FamilySummary 增加代表的 edition_language、original、playable。

精确商品编号搜索优先该商品的实际副本，不被账号偏好替换。编号只存在于关联商品、没有符合筛选的服务器副本时，family.requested_code 标明该商品，客户端打开对应的关联资料，不生成虚假 work ID 或自动播放其他版本。直接访问 work ID、文件关联、播放历史及手动选择的副本始终保持原目标。个人选择接口见 presentation.md。
