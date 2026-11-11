# 片段版本与字幕关联

当前资源关系服从节点唯一归属；编辑不会冻结目录成员。

本模块只建立资源关系，不提供播放、字幕正文解析或容器内部音轨探测。所有路径相对 /api/v1，均需有效会话，网页使用同源 Cookie，原生和外部客户端使用 Bearer。读取允许普通已登录用户，编辑需要管理员。身份为不透明字符串；所有写入使用父作品 ETag/If-Match，返回 work_revision 字符串；分页默认 limit=50，范围 1..500。PUT 是完整替换，字段省略取默认值，force=true 只用于明确处理锁定资源，不绕过范围/修订/命名约束。

## 逻辑片段及视角版本

层次为作品 → 播放组 → segment（逻辑片段）→ rendition（视角/演绎版本）→ 文件成员。现阶段完全显式建立，不根据 Track 编号、视角标签或具体作品名自动生成关联。

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET / POST | /works/{work}/segments | 分页/创建片段 |
| GET / PUT / DELETE | /works/{work}/segments/{segment} | 片段 CRUD |
| GET / POST | /works/{work}/renditions | 分页/创建版本 |
| GET / PUT / DELETE | /works/{work}/renditions/{rendition} | 版本 CRUD |
| GET | /works/{work}/renditions/{rendition}/members | 版本下的文件成员分页 |
| PUT / DELETE | /works/{work}/renditions/{rendition}/members/{member} | 绑定/解除文件；PUT 正文为 {} |

Segment 输入：playback_group_id、name 必填，sort_order 默认 0。响应增加 id、work_id、work_revision。片段属于一个播放组；不跨组按相同编号推断同片段。名称保留用户原文，不强制翻译。

Rendition 输入：segment_id、name 必填，sort_order 默认 0，is_default 默认 false。响应增加 id、work_id、work_revision。segment_id 更新时不可更换；同片段版本名唯一，最多一个默认版本，也允许暂未指定默认。切换默认需先取消原默认，再设置目标，；当前不提供专用默认切换接口。

同作品文件当前最多绑定一个版本，PUT 到另一版本会重绑。一个版本可以有多个格式/文件，不要求 MP3/WAV 名单对应，也不限制每种格式只有一份资源。绑定要求已登记的文件节点、included 音视频成员，且成员播放组与片段组一致。返回 work_id、rendition_id、member_id、work_revision。改变已绑定成员的组/类型或排除成员前，须先解除绑定；更新绑定的段组同样要求先解绑。

例如显式建立 Track4，再建立视角 A/B 两个版本，将 A 的 MP3/WAV 绑定到 A、B 的文件绑定到 B。其他片段不需要有同样的视角/格式集合。

片段/版本/绑定编辑不改变目录边界。删除版本或片段级联清理当前及历史绑定，不删除资源或媒体；锁定资源需 force。解除人工边界不清理播放结构。目录归纳撤回归属时，原绑定保留为 active=false 历史；成员回原作品且组范围满足时恢复 active=true。绑定列表增加 active，表示当前绑定有效，不承诺文件 present。DELETE 绑定可同时移除历史；显式重新绑定会清除该成员其他历史绑定。

## 外置字幕

| 方法 | 路径 | 用途 |
| --- | --- | --- |
| GET / POST | /works/{work}/subtitle-links | 字幕关联分页/创建 |
| GET / PUT / DELETE | /works/{work}/subtitle-links/{link} | 字幕关联 CRUD |

GET 分页支持 media_member_id、subtitle_member_id、state、origin 过滤。播放客户端可按媒体成员查询，避免读取整部作品的全部字幕关系。

输入：media_member_id、subtitle_member_id 必填；locale 默认为 und，显式指定使用 BCP 47；origin 默认为 manual，也可 ai；state 默认为 included，也可 excluded。origin=rule 只由服务器自动生成，客户端不能冒充。一个媒体可以有多个字幕，一个字幕可以关联多个媒体，pair 在同作品内唯一；修改现有配对使用 PUT。

响应增加 id、work_id、work_revision、media_availability、subtitle_availability、usable。availability 为索引观察的 present/missing/unknown；usable 仅表示关联及两个成员 included、两端均为索引中 present 的文件，不保证正文或媒体解码成功。可用性随扫描变化，不一定改变作品修订；响应禁用缓存。

### 命名与简繁日英字幕

支持 .srt、.vtt、.ass、.ssa、.lrc、.txt。媒体主体按原始字节精确比较，不做大小写折叠、全半角转换或模糊匹配；扩展名和语言后缀识别忽略大小写。

先尝试没有剥离语言后缀的完整主体，找不到对应媒体时才识别最后一个点分隔的语言后缀。每一步都优先追加式（`01.wav.srt`），其次替换式（`01.srt`）。例如 `01.wav.cht.srt` 与 `01.cht.srt` 都可以匹配 `01.wav`；同目录同时存在 `01.wav`、`01.wav.mp3` 时，`01.wav.cht.srt` 只自动关联 `01.wav`。同时存在 `01.mp3`、`01.en.mp3` 时，`01.en.srt` 属于后者，语言为 und，避免把真实标题误判为语言。

| 语言 | 支持的后缀（单个点分隔字段） | 自动 locale |
| --- | --- | --- |
| 简体中文 | zh-Hans、zh-CN、zh-SG、chs、sc、简体、简中、简体中文、簡體、簡中、簡體中文 | zh-Hans |
| 繁体中文 | zh-Hant、zh-TW、zh-HK、zh-MO、cht、tc、繁体、繁體、繁中、繁体中文、繁體中文 | zh-Hant |
| 日文 | ja、jp、jpn、ja-JP、日文、日语、日語、日本語 | ja |
| 英文 | en、eng、en-US、en-GB、英文、英语、英語 | en |
| 未注明简繁的中文 | zh、chi、zho、中文 | zh |

语言后缀中的 `_` 可替代 `-`，如 zh_TW。未标注语言使用 und；不根据正文、作品语言或界面语言猜测。`01.fr.srt`、`01.en.sdh.srt`、任意标题标签不作为 `01.wav` 的自动字幕；不支持 default/forced/听障标记。`01.mp3.vtt` 也不会匹配 `01.wav`。

人工/AI 关联仍要求同作品及上述可匹配命名，可跨目录。人工输入 locale 仍默认 und，显式值优先；归纳不会覆盖人工/AI 的语言或排除决定。force 不能越过范围和命名约束。

自动关联仅限已纳入同作品、同来源、同目录、present 的文件。自动维护发生在显式作品归纳应用阶段；单独的目录枚举仍只更新索引。不会为人工归纳的作品偷偷加入新字幕成员。格式多份同目录时，替换式字幕可关联多个同主文件名资源。

人工/AI 可以关联同作品其他目录（也可不同来源）的同名文件，但两端必须已经是作品 included 成员，且必须是已登记的文件节点；外部不透明、未登记的节点无法验证名字，不能建立字幕关系。

字幕关联属于独立资源注解，创建/编辑不会固定作品成员，不改变目录边界。重建只替换 rule 关系，保留人工/AI 的指定、locale 与明确排除；为了保存人工字幕关系，已不在自动结果中的成员可留 excluded 记录，关系变为不可用；候选消失时保留 inactive 作品，不影响纯自动归纳的边界。

更新自动关系时默认转为 manual。要长期拒绝一个自动配对，PUT 为 state=excluded；DELETE 物理移除关系，不留下自动抑制，下次归纳仍可能重建同目录精确配对。缺失只影响 availability/usable，不删除人工关联。解除人工边界只删除相关目录/文件决定；下一次归纳维护自动配对，保留人工/AI 注解。成员被转移或排除期间旧注解不可用，重新归入原作品后可恢复；不保留跨作品有效关系。

本关联模块不读取正文或通过正文判断语言，不关联内嵌字幕，也不提供字幕默认选择或多视角自动关联。正文读取及内嵌字幕由独立播放接口提供。

自动归纳响应中的 updated_works/unchanged_works 统计自动成员和分组投影；字幕注解维护可独立更新父作品修订。归纳完成后按来源/作品刷新字幕关系，不把 unchanged_works 当成“所有资源注解均未变化”。
