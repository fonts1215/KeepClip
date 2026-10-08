# KeepClip — icona ufficiale

Icona selezionata: proposta 3, due fogli sovrapposti corallo e ambra.

`KeepClip.master.png` conserva il PNG trasparente originale (1254 × 1254), creato con lo strumento integrato image_gen. Gli asset derivati mantengono il disegno e il canale alpha; vengono solo ridimensionati e confezionati in ICO da `scripts/Generate-AppIcons.ps1`.

## Rigenerazione

Dalla cartella del progetto:

```powershell
./scripts/Generate-AppIcons.ps1
```

Output in `src/KeepClip.App/Assets`: AppLogo.png (256 px), KeepClip.ico (16/20/24/32/40/48/64/96/256 px), StoreLogo e Square44x44Logo/Square150x150Logo alle scale 100/125/150/200/250/300/400, più le varianti targetsize trasparenti standard/unplated/lightunplated. Le varianti per i temi chiaro e scuro mantengono gli stessi colori.

L'ICO è incluso nell'eseguibile, nell'area di notifica e nelle finestre. AppLogo.png compare nell'header. Il manifest MSIX usa i PNG pubblicati: Build-Package.ps1 verifica la presenza degli asset senza sostituirli.

Dimensioni e integrazione seguono la documentazione Microsoft: https://learn.microsoft.com/en-us/windows/apps/design/iconography/app-icon-construction e https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.windowing.appwindow.seticon .

## Prompt finale

Edit this existing app icon into a restrained professional identity mark that looks deliberately designed by a human graphic designer in a vector editor. Deliver ONE finished square raster icon with actual transparent background. Entirely FLAT two-dimensional vector-like geometry. Solid colors only, maximum three colors, exact smooth edges, crisp clean shapes. Absolutely NO gradients, shading, 3D, inflated/puffy forms, highlights, glowing edges, plastic materials, shadows, textures, decorative flourishes, mockup or scene. The shape should occupy about 75% of the square with clean equal margins and remain clear at 24 px. No letters, words, captions, watermark or text. Keep only the paired-page copy concept and coral/amber palette. Redesign as two clean flat offset rounded-square page frames, back frame coral in upper-left and front frame amber in lower-right, with a tidy overlap and generous transparent negative space inside the frames. Strokes identical in thickness. Remove the ribbon entirely. A confident geometric copy mark, not decorative illustration.

