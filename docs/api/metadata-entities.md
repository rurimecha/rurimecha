# 元数据别名及高级筛选

`/metadata-entities` 支持 GET 分页（q、kind、limit、offset）和 POST 创建；`/metadata-entities/{entity}` 支持 GET、PUT 完整替换和 DELETE。读取/写入返回 ETag，更新/删除要求 If-Match。读取允许普通已登录用户；创建、修改和删除需要管理员会话。

```json
{"kind":"person","name":"规范显示名","aliases":[{"provider":"dlsite","external_id":"provider-person-id","name":"原名","locale":"ja"},{"provider":"manual","external_id":"","name":"另一名字","locale":"zh-Hant"}]}
```

kind 为 person、circle、tag。name 非空，aliases 1..64 项；每项 provider 为 manual、dlsite、asmr.one、fanza，locale 是 BCP 47（省略/空为 und）。外部编号作为证据保存，不自动推断同人，不合并作品。当前实体由管理员明确维护，不自动从同名署名推断人物身份。

实体是检索别名表，不改写作品原始 credits/tags。别名 name 精确匹配署名/标签；person 匹配非 circle 署名、circle 匹配 circle 署名。来源别名要求作品当前编号的共享刮削证据来自相同 provider；manual 别名表示管理员明确允许匹配所有来源。external_id 不被误当作所有同名署名的身份保证。删除实体不删除作品资料。

GET /works 增加可组合参数：tag、credit、role、language、text_locale、provider、entity_id、released_from、released_to。不同条件取交集；credit 与 role 必须匹配同一署名。tag/credit 精确匹配原值，language/text_locale 是规范化后的精确语言，不使用界面显示回退（例如指定 zh-Hant 匹配繁体文本）。provider 按作品编号命名空间筛选 dlsite/fanza。

日期为 YYYY-MM-DD，包含端点；只有年/月精度的发布日期按该年/月首日参与比较。entity_id 使用上述来源别名匹配。q 仍是字面子串，额外支持实体规范名/别名检索，再按实体别名匹配作品；不执行查询运算符，也不自动简繁转写。

GET /works/facets 接收相同过滤条件，返回 total、tags、credits、languages、providers 和 truncated。各值含 value/count，count 为去重作品数；同一数据库快照内统计，按数量降序，每类最多 100 项。limit/offset 不影响聚合范围。仍可用标准 /works 分页读取实际结果。

人物/社团 entity_id 与 role 同时使用时，别名命中和角色要求作用于同一署名；不会因作品另有一个无关声优而将插画实体计入声优结果。标签实体与角色仍是作品级组合条件。

## 作品库分类

`GET /works` 和 `/works/facets` 接收可组合的 `library_type=doujin|asmr`。同人音声为已有任一合法商品编号的作品：dlsite 命名空间的 RJ/BJ/VJ + 6 或 8 位数字，或历史 fanza 的 D_ + 6–12 位数字；标准化忽略大小写和首尾空白。其他作品为 ASMR。无效编号不算编号，多个合法编号发生冲突仍属于同人音声；不要求刮削成功、不使用 identification_state。历史 FANZA 编号参与展示分类不代表恢复其刮削支持。

分类在服务端分页、总数和分面统计之前过滤，与其他条件取交集；新增或修改编号后即时重新计算，无附属表或迁移。省略参数兼容查询所有作品，非法值返回 400。浏览偏好可保存同名筛选。网页默认同人音声；ASMR 不提供作品编号排序。


## 独立标签、声优与社团目录

`GET /works/facet-values?kind=tag|voice_actor|circle` 独立浏览全部有效作品，不继承作品库的分类、搜索、来源、收藏集等筛选。不接受这些作品过滤参数。支持 `q`（名称/显式别名字面子串，NFKC/大小写折叠）、`sort=count|name`（默认作品数降序，名称及实体ID打破平局）、`limit`（默认50，1..500）与 `offset`（默认0）。搜索先于分页，无旧分面的100/500项截断。

响应为 `{items:[{name,count,entity_id,aliases:[]}],total,limit,offset}`。`total` 是搜索后目录条目数，`count` 是每个条目的去重有效作品数。返回列表及计数来自同一快照。声优只统计 `voice_actor`，社团只统计 `circle`，不混入插画或其他制作。词条的多个别名命中同一作品只计一次，不合并作品。

非空 `entity_id` 的条目用 `/works?entity_id=...` 浏览，声优/社团再传对应 `role`；无实体的标签用 `tag=name`，声优/社团用 `credit=name&role=...`。别名仍遵循上述来源证据规则；如果某原始名有不能归入实体的作品，保留原始名条目并计算该精确名称的全部命中，保证列表数量与点击结果一致。多个实体或原始名之间可以重叠，不得把条目数量相加当作全库作品总量。`aliases` 是去重显式别名，不包含规范名称。

网页提供标签、声优、社团三个独立入口；点击条目进入“全部”分类的作品结果并清除旧筛选。浏览作品库本身仍默认同人音声；空 `library_type` 可在浏览偏好中保存全部分类，接口查询可省略该参数。
