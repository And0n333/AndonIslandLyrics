---
name: api-doc
description: DynamicIslandLyrics 外部 API 接口文档和调用参考
disable_model_invocation: true
---

# API Documentation — DynamicIslandLyrics

## 外部 API 总览

本项目通过 4 个外部歌词接口获取同步歌词，优先级如下：

1. AMLL TTML 歌词站
2. QQ 音乐 API
3. 网易云音乐 API
4. LRCLIB 在线接口

---

## 1. AMLL TTML 歌词站

### 搜索
- **URL**: `POST https://amlldb.bikonoo.com/api/search-lyrics`
- **Body**: `{ "title": "<title>", "artist": "<artist>" }`
- **返回**: `{ "results": [{ "id": "<file>", "title": "...", "artist": "...", "duration": <seconds> }] }`
- **实现**: `AmllTtmlLyricProvider.SearchAsync()`

### 下载
- **URL**: `GET https://amlldb.bikonoo.com/raw-lyrics/{file}`
- **返回**: TTML XML 文本
- **实现**: `AmllTtmlLyricProvider.DownloadTtmlAsync()`

### 解析
- **类**: `TtmlLyricParser`
- **格式**: `<p begin="00:01.20" end="00:05.30">歌词文本</p>`
- **忽略**: `ttm:role="x-translation"` 的翻译元素
- **字段**: `ttm:agent="v1"` 标记主歌词

---

## 2. QQ 音乐 API

### 搜索歌曲
- **URL**: `POST https://u.y.qq.com/cgi-bin/musicu.fcg`
- **Body**: JSON 格式，含 `{"method":"DoSearchForQQMusicDesktop","param":{"query": "<query>"}}`
- **返回**: JSON，取 `data.body.song.list[].mid` 为歌曲 mid
- **实现**: `QQMusicLyricClient.SearchSongMidAsync()`

### 获取歌词
- **URL**: `GET https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg`
- **参数**: `songmid=<mid>`, `g_tk=5381`, `loginUin=0`, `hostUin=0`, `format=json`, `inCharset=utf8`, `outCharset=utf-8`
- **注意**: 返回 JSONP 格式，需用 `UnwrapJsonp` 提取 JSON 字符串
- **返回**: `{ "retcode": 0, "lyric": "<base64编码的LRC歌词>", "trans": "<base64编码的翻译>" }`
- **实现**: `QQMusicLyricClient.GetLyricTextAsync()`

### 歌手中文处理
- **方法**: `ReadQQSingerText()` — 读取 `singer` 数组中 `name` 字段，多个歌手用 `/` 分隔

---

## 3. 网易云音乐 API

### 加密方式
使用自定义 WeApi 加密（RSA + AES）：
- **SecretKey**: 16 字节随机字符串
- **EncSecKey**: RSA 加密（公钥 `e0b509...`，固定 hex 公钥）
- **EncText**: AES-128-CBC 加密（key=secretKey, iv="060708...")
- **实现**: `NetEaseLyricClient.Prepare()`, `RsaEncode()`, `AesEncode()`

### 搜索歌曲
- **URL**: `POST https://music.163.com/weapi/cloudsearch/get/web`
- **参数明文**: `{ "s": "<artist> <title>", "type": "1", "limit": "10", "offset": "0" }`
- **返回**: `result.songs[].id`（整数歌曲 ID）
- **实现**: `NetEaseLyricClient.SearchSongIdAsync()`

### 获取歌词
- **URL**: `POST https://music.163.com/weapi/song/lyric`
- **参数明文**: `{ "id": "<songId>", "lv": "-1", "kv": "-1", "tv": "-1" }`
- **返回**: `lrc.lyric`（纯文本 LRC）、`tlyric.lyric`（翻译）
- **实现**: `NetEaseLyricClient.GetLyricTextAsync()`

---

## 4. LRCLIB

### 精确获取
- **URL**: `GET https://lrclib.net/api/get/<id>`
- **返回**: `{ "id": ..., "title": "...", "artist": "...", "syncedLyrics": "[00:01.20]..." }`

### 搜索
- **URL**: `GET https://lrclib.net/api/search?q=<query>`
- **返回**: 搜索结果数组
- **User-Agent**: `DynamicIslandLyrics/1.0`（必需，否则 403）

---

## 通用规则

- 所有 HTTP 请求使用 `System.Text.Json` 解析
- 歌曲时长用于过滤候选结果（`GetMatchScore()` 按匹配度排序）
- 搜索结果用 `BaseMusicClient.GetMatchScore()` 评分：CJK 字符优先精确匹配，拉丁字符允许大小写/变体
- 歌词文本统一用 `TimedLyricLine` 模型：`{ Time, Text, EndTime? }`
- 接口失败自动降级到下一优先级
