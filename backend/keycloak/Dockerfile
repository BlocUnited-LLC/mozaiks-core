# Custom Keycloak image with the Mozaiks login theme baked in.
# Base version should match the running Keycloak version.
FROM quay.io/keycloak/keycloak:24.0

# Copy the theme into the Keycloak themes directory.
COPY theme/mozaiks /opt/keycloak/themes/mozaiks

# Nothing else needed for themes.
