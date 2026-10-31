# CookHub

A personal recipe box, weekly meal planner, shopping list and pantry tracker for a home network.

Built with ASP.NET Core Blazor Server (.NET 10), MudBlazor 9, EF Core 10 and SQL Server. Recipes can be imported from a web page with the Claude API.

> CookHub is meant for **personal use** on your own home server.

## Contents

- [Features](#features)
- [Running it locally](#running-it-locally)
- [Setting up the Claude API key in Visual Studio](#setting-up-the-claude-api-key-in-visual-studio)
- [Deploying to an Ubuntu home server](#deploying-to-an-ubuntu-home-server)
- [Updating the app after it's deployed](#updating-the-app-after-its-deployed)
- [Tests](#tests)
- [Design notes](#design-notes)
- [Project layout](#project-layout)

## Features

- **Accounts**: every page requires login. Anyone on the network can register, with no email confirmation, and is signed straight in.
- **Recipes**: title, description (up to 500 characters), an optional photo (a stock image is used otherwise), ingredients, steps, tags and "Liked by".
  - Each ingredient line is an *ingredient* with an optional *amount* and optional *preparation*, e.g. `1 cup` · `carrot` · `diced`, or just `salt`.
  - Ingredients and tags are looked up by name. If a name doesn't exist yet, the autocomplete offers **Add "…"**, which saves it so it can be picked next time.
  - "Liked by" lists you and your family members as checkboxes, on both the edit page and the recipe page.
- **Add from link**: on the **New recipe** page, paste a link to a recipe on any website and choose **Add recipe**. Claude reads the page, converts the recipe to CookHub's format (amounts, ingredients, preparation, steps and tags), and fills in the form. Check it over, then save. Requires a Claude API key ([see below](#setting-up-the-claude-api-key-in-visual-studio)).
- **Recipe list**: card or list view, search (title, description, tag or ingredient), filters for tags and "Liked by", sorting by title, newest or **most ingredients on hand**, and 50 recipes per page.
- **Family**: add, rename and remove family members.
- **On hand**: tick ingredients on a recipe or the shopping list, or add them directly. Remove anything you've run out of.
- **Meal plan**: weeks run Monday to Sunday. Add recipes to any week, optionally on a specific day.
- **Shopping list**: everything the week's recipes need that isn't on hand. Tick items as you get them. You can group by recipe and show the items you already have.
- **Home**: this week at a glance, plus suggestions: recipes you mostly have the ingredients for that aren't planned yet.

## Running it locally

### 1. SQL Server

Any SQL Server 2019+ edition works, including the free Express or Developer editions. On Windows, install SQL Server Developer or Express edition from Microsoft. Developer edition works with the default connection string below; Express installs as `localhost\SQLEXPRESS`, so use `Server=localhost\SQLEXPRESS` in the connection string. Or, if you have Docker on your PC, run one in a container:

```bash
docker run -d --name cookhub-sql -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=<a strong password>' \
  -p 1433:1433 -v cookhub-sql:/var/opt/mssql mcr.microsoft.com/mssql/server:2022-latest
```

### 2. Connection string

The default in `src/CookHub/appsettings.json` uses Windows authentication against `localhost`:

```
Server=localhost;Database=CookHub;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true
```

To use a SQL login instead, override it without editing the file. Use user secrets for development (see the next section; the key is `ConnectionStrings:DefaultConnection`) or an environment variable on the server.

### 3. Run

Open `CookHub.slnx` in Visual Studio and press **F5**, or from a terminal:

```bash
dotnet run --project src/CookHub
```

On startup the app creates or updates the database by applying EF Core migrations. To manage migrations yourself instead, set `Database:MigrateOnStartup` to `false` and run:

```bash
dotnet tool restore
dotnet ef database update --project src/CookHub
```

Then browse to the URL it prints, choose **Create an account** and start adding recipes.

## Setting up the Claude API key in Visual Studio

The **Add from link** feature calls the Claude API, which needs an API key. Keep the key out of the code and out of git: in development it lives in your **user secrets** file (`secrets.json`), which is stored in your Windows user profile, not in the project folder. On the server it lives in an environment file ([see below](#4-create-the-settings-file)).

### 1. Get an API key

1. Sign in to the Claude Console at <https://platform.claude.com>.
2. Add a payment method or credits under **Billing**. Each import is one Claude request and costs a few cents.
3. Open **API keys**, choose **Create key**, name it e.g. `CookHub`, and copy the key (it starts with `sk-ant-`). You can't view it again later.

### 2. Add it to user secrets

1. Open `CookHub.slnx` in Visual Studio.
2. In **Solution Explorer**, right-click the **CookHub** project (not the solution or the tests) and choose **Manage User Secrets**.
3. Visual Studio opens `secrets.json`. Replace its contents with the following, pasting in your key:

   ```json
   {
     "Anthropic": {
       "ApiKey": "sk-ant-your-key-here"
     }
   }
   ```

   If you also keep a connection string here, put both in the same file:

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost,1433;Database=CookHub;User Id=sa;Password=<password>;TrustServerCertificate=True;MultipleActiveResultSets=true"
     },
     "Anthropic": {
       "ApiKey": "sk-ant-your-key-here"
     }
   }
   ```

4. Save the file and run the app (**F5**). Go to **Recipes → New recipe**, paste a recipe link into **Add from link** and choose **Add recipe**.

The file is saved at `%APPDATA%\Microsoft\UserSecrets\aspnet-CookHub-65410eaf-ccfd-4dd8-9abd-8847988c216f\secrets.json`. You can do the same from a terminal:

```bash
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-your-key-here" --project src/CookHub
```

User secrets are only read when the app runs in the `Development` environment, which is what Visual Studio uses. A published app on the server ignores them.

### Optional settings

| Setting | Default | Meaning |
|---|---|---|
| `Anthropic:ApiKey` | *(none)* | Your Claude API key. Without it, **Add from link** shows a message asking for one; everything else works. |
| `Anthropic:Model` | `claude-opus-5-5` | The Claude model used to read recipe pages. |

### How importing works

CookHub sends Claude the link along with instructions describing CookHub's recipe format. Claude fetches the page itself with its web fetch tool, which is limited to the recipe's own website, and replies with the recipe as structured JSON. CookHub then looks up each ingredient and tag by name, adds any new ones to the shared lists, and fills in the form. Nothing is saved until you choose **Save recipe**. Some websites block automated readers; if that happens you'll see a message and can enter the recipe by hand.

## Deploying to an Ubuntu home server

These steps assume Ubuntu Server 24.04 LTS, a user with `sudo`, and that the server can reach the internet. Commands are run on the server unless stated otherwise. CookHub will be built from git on the server, installed to `/opt/cookhub`, and run as a systemd service under its own `cookhub` user.

SQL Server is installed directly on Ubuntu from Microsoft's package repository; no Docker is needed. SQL Server needs a 64-bit Intel or AMD processor (not a Raspberry Pi or other ARM board) and at least 2 GB of memory.

### 1. Install the .NET 10 SDK and git

```bash
sudo apt update
```

```bash
sudo apt install -y dotnet-sdk-10.0 git curl
```

```bash
dotnet --version
```

The SDK includes the ASP.NET Core runtime, so the server can both build and run the app. (If `dotnet-sdk-10.0` isn't found on your Ubuntu version, follow Microsoft's instructions at <https://learn.microsoft.com/dotnet/core/install/linux-ubuntu>.)

### 2. Install SQL Server

SQL Server 2025 supports Ubuntu 24.04. First add Microsoft's signing key and package repositories (one for SQL Server, one for its command-line tools):

```bash
curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | sudo gpg --dearmor --yes -o /usr/share/keyrings/microsoft-prod.gpg
```

```bash
curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/mssql-server-2025.list | sudo tee /etc/apt/sources.list.d/mssql-server-2025.list
```

```bash
curl -fsSL https://packages.microsoft.com/config/ubuntu/24.04/prod.list | sudo tee /etc/apt/sources.list.d/mssql-release.list
```

Install SQL Server:

```bash
sudo apt update
```

```bash
sudo apt install -y mssql-server
```

Then run its setup:

```bash
sudo /opt/mssql/bin/mssql-conf setup
```

When it asks:

- **Edition**: choose **Express**. It's free, allowed for this kind of use, and handles a family recipe box easily (databases up to 50 GB).
- **License terms**: type `Yes`.
- **System administrator (sa) password**: choose a strong password (at least 8 characters with upper case, lower case and a digit or symbol). Keep it for step 4.

Check that it's running. It starts automatically on boot from now on.

```bash
systemctl status mssql-server --no-pager
```

Install the command-line tools, used for backups below:

```bash
sudo ACCEPT_EULA=Y apt install -y mssql-tools18 unixodbc-dev
```

SQL Server listens on port 1433. CookHub connects to it on the same machine, so **don't** open that port in the firewall.

### 3. Get the code and publish it

Create the service user. Its home folder, `/var/lib/cookhub`, is where ASP.NET Core keeps the keys that encrypt login cookies, so people stay logged in across restarts.

```bash
sudo useradd --system --create-home --home-dir /var/lib/cookhub --shell /usr/sbin/nologin cookhub
```

Clone the repository into your home folder. For a private repository, use a GitHub [personal access token](https://github.com/settings/tokens) as the password, or add an SSH deploy key.

```bash
git clone https://github.com/davemysta/cookhub.git ~/cookhub
```

Create the install folder, owned by you so you can publish without `sudo` (the `cookhub` user only needs to read it):

```bash
sudo mkdir -p /opt/cookhub
```

```bash
sudo chown $USER:$USER /opt/cookhub
```

```bash
cd ~/cookhub
```

```bash
dotnet publish src/CookHub -c Release -o /opt/cookhub
```

### 4. Create the settings file

Settings that differ on the server, including secrets, go in an environment file that only root can read. systemd reads it when starting the service. In environment variables, `__` (two underscores) stands for the `:` in a setting name.

```bash
sudo mkdir -p /etc/cookhub
```

```bash
sudo nano /etc/cookhub/cookhub.env
```

Paste the following, filling in your SQL password and Claude API key, then save with **Ctrl+O**, **Enter**, **Ctrl+X**:

```ini
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5000
ConnectionStrings__DefaultConnection=Server=localhost,1433;Database=CookHub;User Id=sa;Password=<a strong password>;TrustServerCertificate=True;MultipleActiveResultSets=true
Anthropic__ApiKey=sk-ant-your-key-here
```

Lock it down:

```bash
sudo chmod 600 /etc/cookhub/cookhub.env
```

To change the key later, edit this file and run `sudo systemctl restart cookhub`.

### 5. Create the systemd service

```bash
sudo nano /etc/systemd/system/cookhub.service
```

Paste:

```ini
[Unit]
Description=CookHub
After=network-online.target mssql-server.service
Wants=network-online.target mssql-server.service

[Service]
User=cookhub
WorkingDirectory=/opt/cookhub
ExecStart=/usr/bin/dotnet /opt/cookhub/CookHub.dll
EnvironmentFile=/etc/cookhub/cookhub.env
Environment=DOTNET_NOLOGO=1
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=cookhub

[Install]
WantedBy=multi-user.target
```

Start it, and have it start on boot:

```bash
sudo systemctl daemon-reload
```

```bash
sudo systemctl enable --now cookhub
```

```bash
systemctl status cookhub
```

On first start the app creates the database. If SQL Server is still starting up, the service fails and systemd retries every 10 seconds until it works. To watch the log:

```bash
journalctl -u cookhub -f
```

### 6. Open it on your network

If the Ubuntu firewall is on, allow the port:

```bash
sudo ufw allow 5000/tcp
```

Find the server's address with `hostname -I`, then browse to `http://<server-ip>:5000` from any device on your network, choose **Create an account**, and you're in.

Things to know:

- **HTTPS.** Plain HTTP is fine on a trusted home network. Don't forward the port to the internet; anyone who can reach the app can register. If you want HTTPS or a friendly name, put a reverse proxy such as Caddy or nginx in front of it.
- **Backups.** Photos are stored in the database (table `RecipeImages`), so backing up the database backs up everything:

  ```bash
  /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P '<a strong password>' -C \
    -Q "BACKUP DATABASE CookHub TO DISK = N'/var/opt/mssql/data/CookHub.bak' WITH INIT"
  ```

  This writes `/var/opt/mssql/data/CookHub.bak`. Copy it somewhere safe, e.g. `sudo cp /var/opt/mssql/data/CookHub.bak ~/` and then off the server. To restore it, stop CookHub (`sudo systemctl stop cookhub`) and run the same command with `RESTORE DATABASE CookHub FROM DISK = N'/var/opt/mssql/data/CookHub.bak' WITH REPLACE`.

## Updating the app after it's deployed

Make and test your changes on your PC, commit them and push to GitHub. Then on the server:

```bash
cd ~/cookhub
```

```bash
git pull
```

```bash
sudo systemctl stop cookhub
```

```bash
dotnet publish src/CookHub -c Release -o /opt/cookhub
```

```bash
sudo systemctl start cookhub
```

```bash
systemctl status cookhub
```

The app applies any new database migrations automatically when it starts, so there's nothing else to run. Take a [backup](#6-open-it-on-your-network) first if an update changes the database. If something goes wrong, check `journalctl -u cookhub -n 100` for the error. To roll back, check out the previous version with `git checkout <commit>` and publish again; note that database migrations aren't undone automatically.

To save typing, put the steps in a script:

```bash
cat > ~/update-cookhub.sh <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
cd ~/cookhub
git pull
sudo systemctl stop cookhub
dotnet publish src/CookHub -c Release -o /opt/cookhub
sudo systemctl start cookhub
systemctl status cookhub --no-pager
EOF
chmod +x ~/update-cookhub.sh
```

After that, updating is just `~/update-cookhub.sh`.

**Publishing from Visual Studio instead.** If you'd rather not build on the server, right-click the CookHub project, choose **Publish → Folder**, set **Target runtime** to `linux-x64` and **Deployment mode** to *Framework-dependent*, and publish. Copy the output folder's contents to the server (e.g. with `scp -r` or WinSCP) into a temporary folder, then run `sudo systemctl stop cookhub`, copy them into `/opt/cookhub` with `sudo cp -r`, and run `sudo systemctl start cookhub`.

## Tests

```bash
dotnet test
```

The tests exercise the service layer (search, filters, on-hand sorting, paging, likes, meal plans, shopping lists, per-user isolation, recipe import) against in-memory SQLite, so they don't need SQL Server. The recipe import tests use a fake Claude API, so they don't need an API key.

## Design notes

Where the requirements left room for interpretation:

- **Recipes, family members, likes, the on-hand list and meal plans belong to the signed-in user.** Each account has its own recipe box. **Ingredients and tags are a shared catalog**, so names typed by any user can be picked by everyone.
- **Tag filter**: when several tags are selected, a recipe must have *all* of them.
- **"Liked by" filter**: *Anyone* (no filter), *Everyone* (liked by you and every family member), *you*, or one family member.
- **"Most ingredients on hand"** ranks by the number of a recipe's ingredients you have. Ties go to the recipe with fewer left to buy.
- **Shopping list**: there's no separate list to maintain. It's worked out from the week's meal plan minus your on-hand ingredients, so ticking an item adds it to On hand. Amounts aren't totalled across recipes; each recipe's amount is listed, because free-text units like "1 cup" and "2 large" can't be added reliably.
- **Ingredient amounts are optional**, for things like salt and pepper. A blank amount is stored as no amount.
- **Ingredient and tag names** are matched ignoring case and extra spaces. Tags are stored in lower case.
- **Add from link** fills in the form but doesn't save, so you can fix anything Claude got wrong. New ingredient and tag names it finds are added to the shared catalog straight away, even if you then don't save the recipe. The photo isn't imported.
- **Photos**: JPEG, PNG, GIF or WebP up to 5 MB. SVG is refused, because it can contain script.
- **Identity UI** is trimmed to login, register, profile (your display name for "Liked by") and change password. Email-based flows are removed because there's no email on a home server: confirmation, password reset, email change, 2FA, passkeys and external logins. Passwords need 6+ characters including a digit.

## Project layout

```
src/CookHub/
  Components/
    Account/        Identity pages (login, register, profile, password)
    Layout/         Main and auth layouts, nav menu, theme
    Pages/          Home, Recipes/*, MealPlan, ShoppingList, OnHand, FamilyMembers
    Shared/         Lookup-or-add autocomplete, week navigator, dialogs, small pieces
  Data/
    Entities/       EF Core entities
    Migrations/
  Services/         Business logic; each operation uses a short-lived DbContext.
                    RecipeImportService calls the Claude API for "Add from link".
tests/CookHub.Tests/
```
