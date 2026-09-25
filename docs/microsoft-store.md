# Microsoft Store submission

The MSIX product is [QPARK Shot, 9NVF4TS6Z0C7](https://partner.microsoft.com/en-us/dashboard/products/9NVF4TS6Z0C7/overview). It is separate from the earlier EXE/MSI draft.

## Package identity

- Name: `16409D.A-PRoduction.QPARKShot`
- Publisher: `CN=03A4E9DA-894C-481E-9085-3E6B70F64825`
- Publisher display name: `D.A - PRoduction`
- Package family: `16409D.A-PRoduction.QPARKShot_3t58q4wb8hec6`
- Submission: `1152921505701974303`

These values were copied from the product identity page. Use all three identity inputs when dispatching `build-windows.yml`; do not substitute Preview identity values.

## Verified package

[Windows build 36103643160](https://github.com/qparkio/QparkShot-windows/actions/runs/36103643160) built commit `6657e77341ba876492424bab213c5ba3f5b4abc3`. Its artifact includes `Store/QPARKShot-1.2.0-x64-Store.msix`, version `1.2.0.0`, Windows Desktop x64, minimum OS `10.0.17763.0`.

SHA-256: `5bb60c081b62196106b9318a29a430231d7a001779c2d26f531d23b56bf7e078`.

The unsigned Store package contains the application and bundled .NET runtime, with no regression test executable or test certificate. Partner Center accepted this package and displayed **Validated** on 2026-09-25. The `runFullTrust` capability requires Microsoft review; the submission includes an explanation of its desktop capture, hotkey, tray, clipboard, share and local-file usage.

Both unpackaged and installed-MSIX regression runs passed 44 checks, including local OCR. Evidence is retained under `build/qa-store-36103643160/`. This does not establish Store certification, publication, Windows client hardware testing or WACK results.

## Listing materials

`marketing/store-metadata.json` contains descriptions, short descriptions, features, keywords and proposed screenshot captions for 12 languages. The first Store submission leaves release notes blank as instructed by the portal. The app package supplies the existing logo. Optional Xbox artwork and trailers do not apply to this Windows Desktop submission.

The installed package test harness produces three actual WPF interface renders per language in `QA/packaged/store/<language>/`. All 36 PNG files are 2088 by 1576 pixels. They show Library/OCR, annotations and review using synthetic demonstration content, without private user screenshots.

The configured price is free. Categories are Utilities + tools and Productivity. The privacy policy is supplied as text directly in Partner Center; its source is `docs/privacy.md`. Support is `docs/support.md` and `work@qpark.io`. Publishing is held until the publisher chooses **Publish now**.

## Draft status on 2026-09-25

- Pricing and availability, Properties, Packages and Store listings show **Complete**. All 12 language rows independently show **Complete**, with three corresponding screenshots each.
- Additional Testing Information is saved, including capture/editor/library/OCR steps and the fact that no login is needed.
- Age ratings are **In Progress**. The completed questionnaire previews IARC/PEGI/Microsoft 3+, ESRB Everyone and Russia 0+. Saving requires the publisher's confirmation of the IARC agreement and legal age; that confirmation is pending.
- Submission options still show **Incomplete**, although reopening the page confirms that the manual publishing hold and the `runFullTrust` explanation were saved. No field validation error is shown. The remaining status has not been resolved and must be rechecked before certification; do not treat capability approval as already granted.
- **Submit for certification** is disabled. No certification submission or publication has occurred.

Saving a draft or validating a package does not submit the app for certification. The browser retains the overview and the age-rating confirmation page for continuation.
