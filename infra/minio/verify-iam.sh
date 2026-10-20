
#!/bin/sh
set -eu

mc alias set app http://minio:9000 \
  "$MINIO_APP_ACCESS_KEY" "$MINIO_APP_SECRET_KEY" >/dev/null

for prefix in uploads documents; do
  object="app/enterprise-documents/$prefix/iam-check-$$.json"

  mc cp /policies/documents-policy.json "$object" >/dev/null
  mc cat "$object" >/dev/null
  mc rm "$object" >/dev/null
done

if mc cp /policies/documents-policy.json \
  "app/enterprise-documents/forbidden/iam-check-$$.json" \
  >/dev/null 2>&1; then

  echo "FAIL: write outside allowed prefixes"
  exit 1
fi

if mc admin user list app >/dev/null 2>&1; then
  echo "FAIL: administrative operation allowed"
  exit 1
fi

echo "IAM permissions: PASS"
