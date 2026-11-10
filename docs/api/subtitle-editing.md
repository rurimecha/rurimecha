# 台本编辑与格式转换

路径相对 `/api/v1`。三个接口均需管理员会话，同源写请求沿用 Origin/Referer 验证。编辑原始媒体节点，珍藏片段使用其源音轨的台本。

## 读取与编辑

`GET /nodes/{node}/subtitle-editor?track=<轨道ID>` 返回完整可编辑正文。省略 track 创建空稿。响应包含 track、revision、language、format、cues、text、warnings。cues 的每句包含 start/end 秒和纯文本 text；纯文本稿使用 text。读取支持 SRT、WebVTT、普通 LRC、ASS/SSA 对话和 TXT。台本最大 2 MiB、5000 句。坏句、超限和无法完整转换的 ASS 绘图或卡拉 OK 返回 422 subtitle_invalid，客户端保留草稿。

`POST /nodes/{node}/subtitle-preview` 输入如下：

```json
{
  "source_track": "external:subtitle-node",
  "source_revision": "read-revision",
  "language": "zh-Hans",
  "format": "srt",
  "document": {
    "cues": [{"start": 0, "end": 2, "text": "台本正文"}],
    "text": "",
    "warnings": []
  }
}
```

新建时 source_track/source_revision 为空。输入仅接受已知字段，JSON 上限 4 MiB。语言支持 und、ja、en、zh、zh-Hans、zh-Hant。zh 表示尚未注明简繁。时间允许重叠，end 必须大于 start。

响应 filename/content 是预计写出的完整 UTF-8 文件，revision 绑定正文、媒体观察和目标当前内容，overwrite 表示目标已存在。warnings 列出样式去除、LRC 元数据丢失、推算结束时间、导出 LRC 丢失结束时间、TXT 去除时间轴、ASS/SSA 默认样式、目标时间精度调整，以及共享字幕文件会影响其他音轨等。客户端展示这些提示并供用户确认。

## 确认保存

`PUT /nodes/{node}/subtitle-editor` 使用同一份输入，并在 `If-Match` 提供带双引号的预览 revision。已存在文件要求 confirm_overwrite=true；存在转换提示时要求 confirm_loss=true。省略前置条件返回428，预览过期或源稿变化返回412，未确认返回409。

同一音轨、语言、格式仅选定一份文件。目标有明确的现有外置台本时使用其实际文件；否则保存至媒体同目录，以“完整媒体文件名.语言.格式”命名。und 省略语言。多个已有匹配文件返回409 subtitle_duplicate，要求先整理重复文件。新建不会覆盖确认后才出现的文件。已存在文件的正文在覆盖前复核；外部软件不参与应用锁，文件系统不提供针对其他任意进程的比较替换事务。

成功响应含 filename、track、indexed。正常保存立即更新文件索引，播放器与 WebDAV 可读取。indexed=false 表示文件已保存但索引待更新，客户端应提示扫描。持久发布记录在启动或下次保存时核对文件后恢复索引。正文是磁盘文件，数据库不保存台本历史。来源删除同步清理待发布记录。

语音正文不因格式转换而发送给模型。纯文本转时间字幕需要用户明确添加和校对时间；LRC 结束时间用下一句或已知媒体末尾推算，未知末尾默认5秒。转换默认保留源格式文件。编辑器输出纯文本，ASS/SSA 采用默认样式与百分之一秒精度，原有格式效果须在预览确认。媒体目录离线、不可写、符号链接目标或文件占用返回 subtitle_write_failed 或现有不可用错误。

## 可选应用内保存

现有文件保存仍是默认行为。SubtitleEditInput新增可选destination：file（默认）或application。application只写Core数据库，不在媒体目录创建或修改文件；适用于只读媒体目录。仍使用原preview/editor接口、预览revision、If-Match和覆盖确认。每个媒体节点、语言和格式保留一个应用内版本，覆盖不影响同语言格式的本地文件。

应用内轨道ID为application:<opaque-id>，列表source=application；播放器通过现有subtitles接口读取，珍藏片段继续复用源音轨台本的时间裁剪。SubtitleEditor、SubtitleEditPreview和SubtitleSaved可返回destination=application。应用内版本保存后立即indexed=true，无需扫描。读取编辑快照时保留原格式和内容，继续编辑默认存回应用内；可显式选file另存媒体目录文件。

管理员可调用DELETE /nodes/{node}/subtitles/{track}删除应用内版本。只接受application:轨道；If-Match使用GET subtitle-editor返回的revision，加双引号。缺失428、过期412、节点/轨道不匹配404，成功204。此接口不删除媒体或外部字幕文件。普通成员可以读取已发布轨道，不能发布、编辑或删除。来源移除时应用内记录随节点清理。


## 手工台本草稿

管理员使用 `POST /transcript-drafts` 从 UTF-8 文字创建纯文本草稿。请求包含 `node_id`、`language`、`text`，可选 `request_key`。正文须非空且最多 2 MiB，返回 201、Location 和草稿 ETag。同账号重复相同 request_key 和输入会得到同一草稿，参数冲突返回 409。仅接受实际媒体节点，保留正文换行。

`GET /nodes/{node}/transcript-drafts` 按 limit/offset 查询该音轨的草稿摘要。`GET /transcript-drafts/{draft}` 返回完整内容和 ETag。`PUT /transcript-drafts/{draft}` 使用当前 If-Match 替换 content，保留已有句子的 ID，锁定句须先解锁再修改。内容为纯文本或带时间的句子，允许手工标注人物、声道和声音事件。`POST /transcript-drafts/{draft}/undo` 按当前修订撤销最近一次保存，修订继续递增。

`POST /transcript-drafts/{draft}/preview` 与 `POST /transcript-drafts/{draft}/publish` 使用草稿 ETag 和选定格式。发布附 preview_revision 及必要的覆盖和格式损失确认。可保存到媒体目录或 Ruri。`POST /transcript-drafts/{draft}/export` 使用相同内容选择，返回文件名、UTF-8 正文和提示，无需写入媒体目录。导出与发布可选择原文或已保存的译文，纯文本须先添加时间轴才能保存为时间字幕。
