# Publish this repository to GitHub

The folder already includes a Git repository on branch `main`, with all historical commits and tags. Do not run `git init` again and do not upload only the loose source files: that would lose the version history.

1. Extract the entire `TranslateUni-GitHub` folder from the ZIP, including its `.git` directory.
2. Sign in to GitHub and create an empty repository named `TranslateUni` at https://github.com/new. Choose public or private. Do not initialize it with a README, .gitignore or license.
3. Open PowerShell in the extracted folder. Replace YOUR_USERNAME below with your GitHub username (or organization).

```powershell
git remote add origin https://github.com/YOUR_USERNAME/TranslateUni.git
git push -u origin main
git push origin --tags
```

Sign in when Git's credential manager prompts. If it asks for a password rather than offering browser login, use a GitHub personal access token with write access to this repository instead of your account password. Never paste tokens into this repository or commit them.

The repository's Commits page shows the version progression. The tag selector lets you browse each version. Tags do not create GitHub Releases automatically; you can later create releases for selected tags and attach the corresponding original ZIP packages.

To configure the author for your own future commits (the import commits remain unchanged):

```powershell
git config user.name "Your name"
git config user.email "Your GitHub email or GitHub noreply address"
```

An additional `TranslateUni-history.bundle` is provided as a portable backup. To reconstruct the repository from it instead of the ZIP, run `git clone ./TranslateUni-history.bundle TranslateUni`, enter that folder, remove its bundle remote with `git remote remove origin`, then add the GitHub origin and push as above.

Official instructions: https://docs.github.com/en/migrations/importing-source-code/using-the-command-line-to-import-source-code/adding-locally-hosted-code-to-github
