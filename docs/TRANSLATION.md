# Translation Tutorial

Thank you for helping translate our application! This guide will walk you through the process of adding or updating translations.

## Prerequisites

- A GitHub account
- Either **Git** OR **GitHub Desktop** installed on your computer
- A text editor (VS Code, Notepad++...)
- Basic familiarity with **GitHub** and **Git**/**GitHub Desktop**

## Step 1: Fork and Clone the Repository

1. **Fork the repository**
   - Go to the main repository page on GitHub
   - Click the "Fork" button in the top-right corner
   - This creates a copy of the repository in your GitHub account

2. **Clone your fork locally**

   ### Option 1: Using Git (command-line)

   ```bash
   git clone https://github.com/your-username/your-repository-name.git
   cd your-repository-name
   ```

   ### Option 2: Using GitHub Desktop
   - Open GitHub Desktop
   - Go to `File` > `Clone Repository`
   - Select the forked repository from your list
   - Choose a local path to clone it to
   - Click **Clone**

## Step 2: Create a New Branch

### Option 1: Using Git

```bash
git checkout -b translation/TWO-LETTER-ISO-REGION
```

### Option 2: Using GitHub Desktop

- Click on the **Current Branch** menu > `New Branch`
- Name the branch: `translation/TWO-LETTER-ISO-REGION`
- Click **Create Branch**

Use the two-letter ISO language code (e.g. hr, fr, de). If the project later supports regional variants, maintainers will handle that.
You can find the codes [here](https://azuliadesigns.com/c-sharp-tutorials/list-net-culture-country-codes/). (`Two Letter ISO Region` Column)

## Step 3: Locate the Resources File

Navigate to the **_Language_** folder (Located inside of `source\AnimusReforged.Altair\Resources\Language`) and find the appropriate .axaml file:

- **For new translations**: Copy `en.axaml` and rename it to `TWO-LETTER-ISO-REGION.axaml`
  - Example: `es.axaml` for Spanish (Spain)
  - Example: `fr.axaml` for French (France)
  - Example: `de.axaml` for German (Germany)

- **For updating existing translations**: Open the existing `TWO-LETTER-ISO-REGION.axaml` file

## Step 4: Edit the Translation File

1. **Open the .axaml file** in your preferred editor
   - JetBrains Rider
   - VS Code

2. **Understanding the file structure**

   ```xml
   <sys:String x:Key="MainView.Navigation.Play">Play</sys:String>
   ```

3. **Translate the values**
   - Only change the text inside `><` tags
   - Keep the `x:Key` attribute unchanged
   - Preserve any placeholders like `{0}`, `{1}`, etc.

## Step 5: Translation Guidelines

### Important Rules

- **DO NOT** change the `x:Key` attributes (these are the keys used in the code)
- **DO NOT** remove or add new entries without discussion
- **DO** preserve formatting placeholders (`{0}`, `{1}`, `NewLine`, etc.)
- **DO** maintain the same tone and style throughout

### Examples

**Good:**

```xml
<sys:String x:Key="ManagePage.DownloadStatus.UpdatesAvailable">{0} update(s) available</sys:String>
```

**Bad (missing placeholder):**

```xml
<sys:String x:Key="ManagePage.DownloadStatus.UpdatesAvailable">update(s) available</sys:String>
```

### Special Characters and Formatting

- Use proper Unicode characters for your language
- If you want to add a newline to the text you can add `&#10;`

## Step 6: Test Your Translation (Optional but Recommended)

If you can build the project locally:

1. **Enable your language in the code**
   - Find the `SupportedLanguages` array in the code (found in `source\AnimusReforged\Utilities\LocalizationHelper.cs`)
   - Uncomment the line for your language or add it if it doesn't exist:

   ```csharp
   /// <summary>
   /// Array of supported cultures. Initially contains only the default language (English),
   /// but can be extended to include additional languages as they are added to the application.
   /// </summary>
   private static readonly CultureInfo[] SupportedLanguages =
   [
       new CultureInfo(DefaultLanguageCode), // English
       // Add more languages here
   ];
   ```

2. **Build the project** to ensure no syntax errors
3. **Run the application** and switch to your language
4. **Check that all strings display correctly** and fit in the UI

**Note:** Don't commit the changes to the `SupportedLanguages` array - this will be handled by the maintainers when your translation is reviewed and ready for release.

## Step 7: Commit Your Changes

### Option 1: Using Git

```bash
git add source/AnimusReforged.Altair/Resources/Language/TWO-LETTER-ISO-REGION.axaml
git commit -m "Add [Language Name] translation (TWO-LETTER-ISO-REGION)"
```

Example:

```bash
git commit -m "Add Croatian translation (hr)"
```

### Option 2: Using GitHub Desktop

- Go to the **Changes** tab
- Make sure your `.axaml` file is selected
- Write a clear commit message (e.g., `Add Croatian translation (hr)`)
- Click **Commit to translation/TWO-LETTER-ISO-REGION**

## Step 8: Push and Create Pull Request

### Option 1: Using Git

```bash
git push origin translation/TWO-LETTER-ISO-REGION
```

### Option 2: Using GitHub Desktop

- Click the **Push origin** button in the top bar

Then:

1. Go to your fork on GitHub
2. Click "Compare & pull request"
3. Use a descriptive title: `"Add [Language] translation"` or `"Update [Language] translation"`
4. In the description, mention:
   - Which language you're translating to
   - Any questions or notes about specific translations
   - Your native language proficiency level

## Step 9: Review Process

- A maintainer will review your translation
- You may be asked to make changes or clarifications
- Once approved, your translation will be merged into the main project
- You'll be credited as a contributor!
