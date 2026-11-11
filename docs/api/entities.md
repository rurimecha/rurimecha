# 按需实体比较

路径省略 `/api/v1`，需有效会话，网页使用同源 Cookie，原生和外部客户端使用 Bearer。能力标识为 `duplicates.identity`。这里只查询操作系统提供的实体证据，不读取或比较文件内容，不自动合并作品、删除文件或追踪移动。

`POST /entities/compare`：

```json
{"left_node_id":"node-a","right_node_id":"node-b"}
```

200 响应包含 left_node_id、right_node_id、same_entity、identity_evidence：

- `same_entity=true` 且 `identity_evidence=os_same_file` 表示操作系统确认指向同一实体，例如硬链接。目录也可比较。
- `unconfirmed` 表示未确认相同；false 不能证明一定是独立实体，不同挂载不保证提供可靠的实体身份。
- `unavailable` 表示其中一端无法实时取得属性。

节点须已登记、present，且来源存在，否则 404；空身份等非法输入为 400。文件系统可能同时变化，结果不是事务性的存在性保证。


不提供文件内容指纹或内容重复组接口。节点 size 和 modified_at 作为扫描属性返回。
