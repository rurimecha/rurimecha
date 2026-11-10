# 按需实体比较

路径省略 `/api/v1`，需 Bearer 认证。能力标识为 `duplicates.identity`。这里只查询操作系统提供的实体证据，不读取或比较文件内容，不自动合并作品、删除文件或追踪移动。

`POST /entities/compare`：

```json
{"left_node_id":"node-a","right_node_id":"node-b"}
```

200 响应包含 left_node_id、right_node_id、same_entity、identity_evidence：

- `same_entity=true` 且 `identity_evidence=os_same_file` 表示操作系统确认指向同一实体，例如硬链接。目录也可比较。
- `unconfirmed` 表示未确认相同；false 不能证明一定是独立实体，不同挂载不保证提供可靠的实体身份。
- `unavailable` 表示其中一端无法实时取得属性。

节点须已登记、present，且来源启用、未退役，否则 404；空身份等非法输入为 400。数据库快照读取结束后才访问文件系统属性，不在文件系统 I/O 期间持有数据库事务。文件系统可能同时变化，结果不是事务性的存在性保证。

文件内容指纹任务、缓存、内容重复组接口及 `duplicates.content` 能力已删除；比较响应不再包含 content 字段。节点 size 和 modified_at 继续作为普通扫描属性返回。节点身份与归纳缓存使用的内部摘要保留，它们不读取媒体正文。
