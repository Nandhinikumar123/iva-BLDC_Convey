#ifndef PNET_VERSION_H
#define PNET_VERSION_H

/* Manually define Git revision (optional) */
#define PNET_GIT_REVISION "dev"

/* If build version not defined, use git revision */
#if !defined(PNET_VERSION_BUILD) && defined(PNET_GIT_REVISION)
#define PNET_VERSION_BUILD PNET_GIT_REVISION
#endif

/* Version numbers (set manually) */
#define PNET_VERSION_MAJOR 1
#define PNET_VERSION_MINOR 0
#define PNET_VERSION_PATCH 0

/* Full version string */
#if defined(PNET_VERSION_BUILD)
#define PNET_VERSION \
   "1.0.0+" PNET_VERSION_BUILD
#else
#define PNET_VERSION \
   "1.0.0"
#endif

#endif /* PNET_VERSION_H */