# Custom Keycloak image with the Mozaiks login theme and realm config baked in.
# Base version should match the running Keycloak version.
FROM quay.io/keycloak/keycloak:24.0

# Copy the theme into the Keycloak themes directory.
COPY theme/mozaiks /opt/keycloak/themes/mozaiks

# Copy the realm import file for automatic provisioning.
COPY mozaiks-realm.json /opt/keycloak/data/import/mozaiks-realm.json
