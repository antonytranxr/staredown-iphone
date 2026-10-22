# Staredown iPhone development

Use Unity 6000.3.26f1 for this iPhone checkout. Preserve its iPhone build/export overrides and Unity `.meta` files when syncing shared changes from MetaVRStarter. Do not copy the Quest project's editor pin or build settings over the iPhone settings.

## Standing publishing preference

The user requested on 2026-10-22 that every new mobile version be uploaded to GitHub automatically. When a new mobile version is completed, commit the validated source and updated version/validation records, then push to the existing `origin/main` at `antonytranxr/staredown-iphone` without asking for permission again. Verify that GitHub's branch points to the pushed commit and report any failure.

Preserve remote changes; do not force-push. Keep generated Xcode projects, app binaries, signing credentials, local preferences and local-only checkpoint tags out of the push. This standing instruction concerns GitHub source publishing; TestFlight uploads are a separate step.
