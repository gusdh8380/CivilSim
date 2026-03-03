# Building Icon Prompts (All Buildings)

## Common style prefix
Use this prefix for every icon:

`Clean city-builder game icon, isometric 3/4 view, stylized low-poly feel, soft toon shading, simple and readable silhouette, centered object, transparent background, no text, no logo, no border, consistent top-left light, high clarity, UI-friendly composition`

## Negative prompt
Use this suffix for every icon:

`photorealistic, realistic human face, watermark, signature, letters, words, logo, frame, busy background, blur, noise, low resolution, over-detailed texture, harsh contrast`

## Output spec
- 1024x1024
- transparent PNG
- object occupies 70 to 80 percent of canvas
- same camera angle and lighting for all icons

## Prompt template
`[Common style prefix], [Building prompt], [Output spec], [Negative prompt]`

## Building prompts

### Residential
- 집: `a compact family house with warm roof colors, small front yard hint, cozy residential look`
- 주거 건물 01: `a low-rise residential apartment block variant 01, clean facade, regular window rhythm`
- 주거 건물 02: `a low-rise residential apartment block variant 02, slightly different roof and facade accents`
- 주거 건물 03: `a low-rise residential apartment block variant 03, modern balcony accents and neat structure`
- 주거 건물 04: `a low-rise residential apartment block variant 04, corner-focused facade and clear silhouette`
- 주거 건물 05: `a low-rise residential apartment block variant 05, brighter wall tones and tidy windows`
- 주거 건물 06: `a low-rise residential apartment block variant 06, compact footprint and distinct roof detail`

### Commercial
- 상점: `a small neighborhood retail shop with clear storefront window and simple awning`
- 상업 건물 01: `a medium commercial building variant 01, street-facing glass frontage and signage shape without text`
- 상업 건물 02: `a medium commercial building variant 02, modern facade with clean entrance canopy`
- 상업 건물 03: `a medium commercial building variant 03, stacked storefront floors and strong silhouette`
- 상업 건물 04: `a medium commercial building variant 04, compact mall-like massing and bright commercial tone`
- 모텔 01: `a compact city motel variant 01, roadside lodging style, clean entrance and simple parking hint`
- 모텔 02: `a compact city motel variant 02, layered facade and hospitality building mood`
- 모텔 03: `a compact city motel variant 03, warm lighting accents and clear lodging silhouette`
- 모텔 04: `a compact city motel variant 04, modern roadside hotel style with neat facade`
- 모텔 05: `a compact city motel variant 05, simple hospitality architecture and visible entry zone`

### Industrial
- 공장: `a basic urban factory with boxy massing, chimney element, and industrial service doors`
- 회사 건물 01: `an industrial office building variant 01, functional facade and utility-like details`
- 회사 건물 02: `an industrial office building variant 02, reinforced structure lines and practical look`
- 회사 건물 03: `an industrial office building variant 03, compact production-office hybrid silhouette`
- 회사 건물 04: `an industrial office building variant 04, sturdy facade and heavier industrial character`

### Public
- Elementary School: `a small modern elementary school, bright facade, civic design, subtle playground hint`
- Community Clinic: `a neighborhood public clinic, clean white and light-blue palette, clear medical entrance`
- Fire Station: `a low-rise fire station with red accents, large garage bay and emergency service feel`

### Utility
- Power Substation: `a compact electrical substation with transformer units, fenced yard feel, utility boxes`
- Water Pump Station: `a municipal water pump station with pipes and tanks, blue utility accents`
- Waste Processing Center: `a small waste processing facility with sorting containers and vent units`

## Batch consistency notes
- Keep one fixed seed for a full category batch.
- Keep same lens and camera tilt for every icon.
- Do not change background style between categories.
- Regenerate any icon whose silhouette is hard to read at 64x64.
