# 离线元数据包

Ruri 接受独立工具产出的 `ruri-metadata` v1 JSON 包。一个包对应一个 RJ/BJ/VJ 商品快照，可以在没有对应媒体、无法连接 DLsite、在线刮削关闭时导入。独立服务当前发现 RJ、按编号支持 RJ/BJ/VJ；同号的本地作品仍有各自身份。

## 导入接口

`POST /api/v1/metadata-packages/import?replace=false`

使用现有 Bearer 或 Cookie 会话；Cookie 写入继续验证 Origin/Referer。Content-Type 为 `application/json`，单次请求最多 64 MiB。请求体直接为下面的包，不包在另一个字段内。不需要 If-Match。

```json
{
  "format": "ruri-metadata",
  "version": 1,
  "metadata": {
    "original_locale": "ja",
    "texts": [{"locale": "ja", "title": "原文标题", "description": "原文介绍", "description_parts": null}],
    "identifiers": [{"namespace": "dlsite", "value": "RJ123456"}]
  },
  "source": {
    "provider": "dlsite",
    "code": "RJ123456",
    "url": "https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html",
    "fetched_at": "2026-11-01T00:00:00Z",
    "raw": {}
  },
  "translations": [{"locale": "zh-Hans", "title": "本地译文标题", "description": "本地译文介绍", "description_parts": null}],
  "images": []
}
```

`metadata` 还支持 `product_info`、`release_date`、`release_date_precision`、`languages`、`credits`、`tags`、`cover_ref`，形状与作品描述一致。它不含 work_id、node_id、成员、播放组或人工覆盖。标准化后元数据最多 8 MiB；旧包可选的 `raw` 为 JSON，最多 12 MiB，导入后舍弃；新导出包不再包含原始响应。`source.provider` 支持 dlsite、asmr.one，编号必须与 identifiers 一致，source.url 必须为允许的 HTTPS 提供者 URL。未知字段、版本、重复或非法语言、无有效标题、错号均拒绝。

`translations` 是本地翻译的独立文本集合，按 BCP 47 保存。不能使用 und 或与 metadata.texts 重复的语言，包括规范化后重复；保留 metadata.original_locale、原文及来源信息。翻译结构化介绍时按语言提供整份 description_parts，不将译文写回原文。接口不调用翻译模型。

`images` 每项形状为 `{"url":"https://img.dlsite.jp/...jpg","data":"<base64原始图片字节>"}`。URL 必须是包中实际引用且属于允许提供者图片域的地址，不能是路径。支持 JPEG/PNG/GIF/WebP，每图最多 16 MiB、每包解码后总计最多 32 MiB，至多 1024 个不同引用。省略图片可以导入文字，但对应图片无法离线显示；空图片集合不清空已存在图片。

成功 `200`：

```json
{"code":"RJ123456","cache_revision":"1","imported":true,"applied_works":2}
```

- 默认 `replace=false`：已有商品缓存复用，不修改其内容或修订；已有有效图片保留，允许补齐缺图。`imported=false` 表示复用，`applied_works` 只计最终描述实际变化的已有作品。重复导入不推进未变化作品的修订。
- `replace=true`：按既有商品缓存合并规则更新明确提供的字段/语言，保留未提供字段；同语言文本可更新。上游 fetched_at 早于已有缓存时返回 `412 stale_revision`，即使包含新译文也不覆盖新快照。已有缓存需要补入译文时，应在最新快照的包上编辑，再显式 replace。
- 一次请求的商品缓存、图片指针和所有同号作品描述原子提交。人工字段、锁定、作品身份和媒体归属保持既有保护。缺少本地作品时只保存缓存，不凭空创建媒体作品；后续归纳/刮削可复用缓存。
- 不发起网络、扫描或模型任务，不创建刮削任务；同步响应即为本次提交结果。客户端取消在提交前使本次事务回滚；响应丢失时可默认重试，之前成功的其他包不回滚。
- 接口没有分页或后台取消接口；每请求一包，批量客户端逐包发送并记录结果。

错误包括 `400 invalid_argument`、`401 unauthorized`、`403 forbidden`、`409 scraper_busy`（等待刮削任务结束后重试）、`409 image_cache_capacity`（提高图片容量限制或清理缓存后重试，不自动淘汰图片）、`412 stale_revision`、`413 request_too_large`、`415 unsupported_media_type`、`503 unavailable`（此服务实例未提供刮削组件）。更多见 [协议](protocol.md) 和 [OpenAPI](openapi.json)。

导入后，作品描述/多语言接口立即返回已生效内容；图片通过已有 `/works/{work}/images` 和认证图片接口读取，不把来源 URL 换成私有文件路径。`GET /info` 的能力标识为 `metadata.packages`。

## 独立服务生成数据包

新 ruri-scrape 通过 `GET /api/v1/products/{code}/package` 提供当前包：本地命中直接返回，缺失时抓取并保存。该服务只提供文字包，images 为空；Core 仍接受其它合法来源提供的带图包。该服务不生成 AI 译文；包仍可携带显式提供的独立 locale 文本。详见 [服务协议](scrape-service.md)。

## 多语言商品资料

`metadata.product_info.localized_metadata` 保存按语言分组的完整商品展示字段与获取记录，结构见 [元数据刮削](metadata-scraping.md)。标题与介绍继续存于同语言 `metadata.texts`。导入、导出保留全部语言；每个语言的署名、标签、系列、价格/币种、封面和其他商品字段互不覆盖。语言记录内禁止再次嵌套 localized_metadata；总大小最多4 MiB，整个标准化元数据仍最多8 MiB。该结构不是媒体身份，不能改变作品成员或音轨/字幕语言。
