# Вторая визуальная редакция

Проверенные снимки из Unity Play Mode: [багажник](Verification-V2/lowpoly-v2-trunk.png), [двигатель](Verification-V2/lowpoly-v2-engine.png), [главное меню](Verification-V2/lowpoly-v2-menu.png), [настройки](Verification-V2/lowpoly-v2-settings.png), [21:9](Verification-V2/lowpoly-v2-wide.png).

Переработаны настоящие элементы Unity UI: фаски и глубина рамок, янтарные кнопки, шрифт Roboto Condensed Bold с кириллицей, выделение слота, приборы, предметы, схема двигателя и фон главного меню. Тексты, показатели и кнопки остаются отдельными рабочими компонентами.

Графика хранится в `Assets/Resources/UI/LowPoly`: `inventory_atlas.png` (9 импортированных Sprite-подобъектов), `engine_bay.png`, `menu_backdrop.png`, `panel_v2.png`, `button_v2.png`, `gauge_v2.png`. Иллюстрация двигателя — схема узлов; реальное состояние установки АКБ выводится отдельным текстом. Фон главного меню — иллюстрация, а не предпросмотр выбранной машины. В гараже остаётся игровой предпросмотр автомобиля.

Иллюстрации созданы встроенным image_gen. Для финального атласа использован запрос:

> A transparent PNG game inventory sprite sheet. Nine isolated 3D low-poly objects on TRANSPARENT alpha background. Exactly 3x3 uniform grid, each object centered with generous padding in equal square cells. Row1: yellow gasoline jerrycan, blue water jerrycan with white droplet, black rugged tire with gray rim. Row2: olive car battery with lightning mark, wood handled steel axe, red toolbox with wrench mark. Row3: blue-gray four-cylinder car engine, olive radiator, scrap metal pile. Clean flat-shaded low-poly style with large matte flat color polygon facets and basic bevels, directional top-left light, dimension and depth, muted dusty colors. Very simple materials, absolutely no scratches no rust texture no grain no realistic PBR. NO checkerboard pattern, NO backdrop color. Real alpha transparency as in isolated product PNG. No text, no grid lines, no UI. All objects entirely contained with ample 15 percent transparent margin in each cell, not touching cell edges.

Финальная редакция двигателя:

> Simplify this engine bay illustration into clean flat-shaded LOW POLY game art. Preserve composition, olive fenders, blue gray engine, radiator bottom, battery right, coolant bottle top right and transparent background. Remove ALL noisy scratches, rust, mottled textures. Use large clean flat polygon planes with restrained bevel shading. Reduce fine mechanical details by 70 percent, chunky readable hoses, broad engine blocks, 12-sided caps. Match beautiful clean low-poly survival game aesthetic, NOT photorealism or high-poly PBR. Keep dimensional form and strong top-left lighting. No text no labels no UI. Entire silhouette within image, 5 percent transparent margin.

Фон меню:

> Production main menu background for low-poly post-apocalyptic car survival game, landscape 16:9 1920x1080. Clean flat shaded low-poly 3D diorama, broad geometric facets, matte colors, no texture noise. An olive armored old sedan with roof cargo rack, spare tire, fuel can, heavy bumper, dusty tires is parked on ruined asphalt in the RIGHT HALF, front three quarter view, hero car large. Behind it deserted angular canyon highway, broken power pylons, abandoned checkpoint, distant faceted mountains and small sand haze. Afternoon warm directional light, desaturated slate blue sky, olive and charcoal car, ochre earth, beautiful warm-cool contrast, atmospheric depth. LEFT THIRD intentionally dark empty shadowed road with subdued silhouettes and no focal objects, for a separate live UI menu to overlay. Premium cohesive low-poly game aesthetic, not photorealism. NO letters, no logos, no buttons, no HUD, no UI, no watermarks. Complete cinematic environment artwork.

Рамки и прибор созданы редакторской командой `RogueDrive / UI / Build Presentation Frames`. Импорт графики воспроизводится через `Tools/Verification/ImportLowPolyPresentation.cs`. Шрифт получен из googlefonts/roboto-2, лицензия сохранена рядом в `Fonts/LICENSE.txt`.
