# ScriptPlayer v1.0.12.3: NFO metadata guide

**NFO files are optional.** Video playback and script synchronization do not require them. Without an NFO title, the library uses the filename. An NFO adds display metadata; it does not replace the actual video or Funscript files.

This guide describes the fields read by the current player, not the full Kodi or Jellyfin metadata schema.

## Create an NFO without writing XML

Add a video to the media library, right-click its entry, and choose the local NFO editor (currently labelled **编辑本地 NFO / EroScripts 标签**). Enter the title, tags and other metadata, then save. Script statistics are optional. Legacy plain-text NFO files remain readable but cannot be silently converted or overwritten by the XML editor; back them up before a separate conversion.

## Filename and location

Place the NFO beside the video, using the same basename:

```text
Demo.mp4
Demo.nfo
Demo-AuthorA.funscript
Demo-AuthorA.twist.funscript
Demo-AuthorB.funscript
Demo-AuthorB.surge.funscript
```

Usually one video-level `Demo.nfo` is enough. The library prefers the video's metadata. When looking up metadata by a script path, the parser tries an exact NFO name before progressively removing trailing hyphen segments. Use a matching video basename rather than relying on fallback naming.

## Minimal example

```xml
<?xml version="1.0" encoding="UTF-8"?>
<movie>
  <title>Demo Video</title>
  <tag>Demo</tag>
  <tag>Multi-axis</tag>
  <favorite>false</favorite>
</movie>
```

Copy `examples/Demo-minimal.nfo`, edit it, and save under your video's basename. Preserve existing NFO files before replacing them.

## Supported fields

All metadata fields are optional. Place these elements directly inside `movie`; names are case-sensitive and the lowercase forms below are recommended.

| Field | Meaning |
| --- | --- |
| `title` | Display title; empty/missing falls back to the filename |
| `originaltitle` | Optional original title |
| `type` | Free-text category, not an axis mapping or device setting |
| `source` | Optional source URL; do not include credentials or private tokens |
| `plot` | Description; CDATA can contain multiline text |
| `favorite` | `true` or `false`; omitted means not a favorite |
| `tag` | One tag per element; repeat for additional tags |
| `scripts` | Optional script-statistics container |

Accepted aliases: `name` for `title`; `original-title` for `originaltitle`; `url`/`website` for `source`; `mediatype` for `type`; `description` for `plot`; `favourite` for `favorite`; and `genre` as a tag. Prefer one standard spelling per field.

## Optional script statistics

Place the following inside `movie`, before its closing tag:

```xml
<scripts>
  <script file="Demo-AuthorA.funscript">
    <duration>00:01:00</duration>
    <durationms>60000</durationms>
    <actions>121</actions>
  </script>
</scripts>
```

`file` identifies the script whose statistics are being described. Use the actual filename, including the extension, rather than an absolute path. Entries without a filename are ignored.

- `duration`: display text, preferably `HH:MM:SS`; existing `MM:SS` is also readable.
- `durationms`: integer milliseconds. The current list displays `duration`; it does not format this integer automatically.
- `actions`: optional integer action count.
- `maxspeed` / `avgspeed`: optional integer speed statistics. They are displayed, not used as device commands. Units are determined by the producing tool; this parser does not normalize or label them.

The example numbers are illustrative. Omit unknown statistics rather than inventing them. NFO statistics are not calculated automatically. A `script` also accepts a `file`/`filename` child instead of the attribute; `actioncount` and `averagespeed` are accepted aliases.

The `scripts` section does not select authors, download scripts, or load axes. Script selection and companion loading still use actual files and player matching rules.

## Editing and refresh

- Save UTF-8 XML with a single `movie` root. Verify the extension is `.nfo`, not `.nfo.txt`.
- Escape `&` as `&amp;` and `<` as `&lt;`, including URL query separators. CDATA is useful for descriptions but cannot contain `]]>`.
- Use one `tag` element per label. The UI may translate known labels without changing the original file.
- Avoid DTDs and extra namespaces; DTD processing is prohibited.
- Unknown XML extensions are normally preserved during metadata editing, but are not automatically supported features. In particular, `thumb`/`poster` are not currently read by this NFO parser to load covers.
- After an external edit, use **重新检查所有条目** (recheck all entries) in the library's More menu. Automatic watching of all external edits is not guaranteed.
- After renaming files, check the NFO basename and its internal `script file` references. Associated-file renaming does not currently migrate those internal references.

The supplied templates use original neutral example data. No personal media, source URLs, configuration, or connection keys are included.
