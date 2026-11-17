# 声优公开资料

基础地址为 `https://api.rurimecha.top`。所有对象支持匿名 GET/HEAD，无需账户、产品授权或访问密钥。此域名不提供发布、上传或修改资料的接口。读取结果为静态 JSON；人物、名称和作品编号不构成完整出演全集或独立身份核验。

## 当前版本

`GET /voice/manifest.json`：

```json
{"format":"ruri-voice-catalog","version":1,"revision":"<sha256>","published_at":"2026-11-17T00:00:00Z","index":"voice/versions/<sha256>/index.json","sha256":"<sha256>","actors":716,"works":0}
```

`revision`为索引文件内容的SHA-256（小写十六进制）；`sha256`同值。`actors`为人物数，`works`为按人物计算的RJ关联数，同一作品参与多位人物时分别计数。`published_at`为本轮完整发布的UTC时间。仅在所引用对象全部上传成功后替换当前版本。

客户端应先取得清单，再从同一域名读取`index`并核对摘要。清单最多缓存300秒；版本化索引、人物和封面可长期缓存，支持ETag条件请求。更新失败可继续使用上次完整取得的版本。

## 检索索引

`GET /voice/versions/{revision}/index.json`：`{format:"ruri-voice-index",version:1,items:[...]}`。

每个条目包含`id`、`name`、`kind`、`names`、`tags`、`traits`、`works`、`profile`、`sha256`。`profile`是相对公开根地址的路径，格式`voice/actors/{id}/{sha256}.json`，摘要针对该文件实际字节。`id`是稳定人物身份，不能用同名替代；`kind`为person/unknown/synthetic。`names`为`{name,locale,kind}`数组，tags为`{key,locale,label}`数组，traits为各语言声音特色字符串数组；客户端可自行按名称、别名和声音特色检索，不需要逐人物查询。

## 人物与作品编号

人物文件包含`id,name,kind,names,texts,tags,updated_at,codes`和可选`cover`。`texts`按语言包含`{locale,description,traits}`；原文分别保存，没有相应语言时由客户端选择显示回退，不生成或覆盖翻译。`codes`为排序并去重的有效RJ编号数组。仅有编号不代表已获取作品详情、ASMR.one已收录或用户拥有媒体。

`cover`存在时为`voice/covers/{sha256}`相对路径，读取返回实际JPEG/PNG/GIF图片；缺少封面时使用客户端占位图。所有文本按纯文本展示，不能直接执行其中的HTML或链接脚本。

人物资料与媒体库身份独立。客户端按完整标准化RJ号匹配已有作品；多个同号作品保留选择。查看这些资料不自动入库、合并人物/作品或下载媒体。未找到的对象返回404，不能据一次请求失败删除本地人物或媒体资料。

## 通过 Ruri Core 读取

Core提供同源、需要Core登录的读取接口。普通成员及已启用的游客可以读取；这不改变远程来源浏览、管理员入库等既有权限。Cloudflare侧仍匿名读取，Core不会转发用户凭据、Cookie或搜索词到公开资料站。

| Core路径（前缀 /api/v1） | 行为 |
| --- | --- |
| GET /voice-actors | q/exact/sort/limit/offset/refresh；检索公开人物索引 |
| GET /voice-actors/{actor} | 取得完整人物及全部发布语言；支持refresh |
| GET /voice-actors/{actor}/works | 分页读取已发现RJ号，批量匹配本库；支持metadata_locale |
| GET /voice-actors/{actor}/works/{code}/target | 点击时重新确认单部作品去向；编号必须属于该人物 |
| GET /voice-actors/{actor}/cover | 代理读取经过摘要验证的人物图片；支持size=thumbnail，无图片404 |

索引默认按名称、ID排序；sort=works按已发现作品数降序，再按名称和ID排序。q进行NFKC、大小写及空白规范化，字面子串搜索姓名、别名、声音特色和标签；exact=true仅匹配姓名与别名，同名可返回多人，不自动选择或合并身份。limit默认50、最大500，offset默认0；先筛选/排序后分页，总数不因超出末页而丢失。q最长512字节。

列表返回`{items,total,limit,offset,source}`，人物详情返回`{actor,source,cover_url}`；cover_url是Core图片地址或空字符串。`actor`保留公开人物原文及其相对图片引用，不改写本库名称、元数据实体、署名或人工资料。现有声优分面及本库作品筛选继续保持原语义。可用exact检索为现有声优名称找到候选，再用明确的公开actor ID读取资料；不提供隐式身份绑定或别名导入。

Core按需获取资料，不在启动时联网或下载全部人物。索引每五分钟检查一次；refresh=true强制本机检查，公开站的边缘缓存仍可能保留五分钟。版本和SHA-256通过后才替换缓存，未变化的索引不重复下载或写入；人物及封面只缓存访问过的对象。重启可使用缓存；刷新失败且存在完整旧缓存时返回旧数据，`source.stale=true`、`source.error`说明失败，`source.revision/published_at`仍属于旧版本。没有可用缓存时，网络失败502 voice_catalog_unavailable、摘要/内容错误502 voice_catalog_invalid、不支持格式502 voice_catalog_unsupported；缓存无法保存503 voice_cache_unavailable。JSON接口Cache-Control为no-store；原图private,max-age=300，缩略图沿用private,no-cache及ETag条件读取。

作品列表仍使用`{items,total,limit,offset,source}`，每项为`{code,title,local_works,target}`。total是该人物发布的RJ号数量，并非本库作品数；没有本库标题时title为RJ号。先对RJ分页，再按当前页完整标准化编号匹配整个本库；同号不同目录、来源和副本分别保留，非活动或已排除的作品不参与。媒体状态`media_present`只说明索引中有音视频，不代表远程网站此刻可访问。所选资料语言没有标题时仍保留作品身份，标题退回RJ号。

`local_works`每项包含id/title/root_node_id/root_path/source_id/source_name/source_kind/media_present，root_path为作品根的相对路径。本地且已索引到媒体的副本优先排列，多个副本始终保留选择。target为：

- `kind=local_work`：一个已入库作品，work_id为其ID。
- `kind=select_work`：有多个已入库副本，work_id为空，客户端展示local_works供选择。
- `kind=remote_search`：未匹配本库，work_id为空，使用remote_search。

所有target均附带`remote_search={source_id:"builtin-asmr-one",query:"RJ...",search_field:"text",path:"/api/v1/sources/builtin-asmr-one/remote-works?search_field=text&q=RJ..."}`，供显式的替代搜索操作使用。不预先请求ASMR.one判断收录，不触发搜索、入库或媒体下载。查看目标仅返回描述；之后访问来源或入库仍按相应权限校验。入库继续调用既有`POST /sources/builtin-asmr-one/remote-works/{code}/import`，归属内置来源；已在用户自建来源入库的作品仍作为本库匹配返回，不迁移或合并。

本库匹配每次从当前快照读取，作品新增、移除或改号后无需刷新公开索引即可反映新去向；单部target接口用于点击时确认，避免把先前页面上的入库状态当作永久状态。上述是Core接口能力，客户端页面需使用这些接口接入。
