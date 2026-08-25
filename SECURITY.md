# Security notes

The application stores user data locally. Credentials for external translation services must be configured by the user and are stored with Windows DPAPI. No service credential is embedded in the application source.

Known exposed credentials from older revisions must be revoked and replaced at the Baidu Translate account level. Removing a credential from the current source does not invalidate a credential that has already been exposed in Git history.