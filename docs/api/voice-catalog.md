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
