# Step-by-step setup guide (for beginners)

Follow these steps in order. Expect about 1–2 hours the first time, plus build and recording time.

**Deadline: 9:30 am, 12 November.** Aim to finish by the evening of the 11th.

---

## Step 1: Install Unity (about 20–40 minutes)

1. Download **Unity Hub** from https://unity.com/download and install it.
2. Open Unity Hub, sign in (a free Personal licence is fine), then go to **Installs → Install Editor**.
3. Choose the latest **Unity 6 LTS** version (6000.0.x).
4. In the modules list, make sure **Microsoft Visual Studio Community** is ticked (it's useful for reading the code).
   *Windows Build Support (Mono)* comes with the Windows editor automatically.
5. Click Install and wait.

## Step 2: Create the project

1. In Unity Hub go to **Projects → New project**.
2. Pick the **Universal 3D** template (sometimes called "3D (URP)").
3. Name it `VACAC-Conveyor-System`, choose a folder (for example `Documents\Unity`), and click **Create project**.

## Step 3: Import VACAC's models

1. With the project open, go to **Assets → Import Package → Custom Package…**
2. Open `Desktop\VACAC_Graduate_Software_Engineer_Take-Home_Technical_Task.unitypackage`.
3. Click **Import** (leave everything ticked).
4. Look in the **Project** window (bottom of the screen) for the new folders and find the conveyor models and product models.
   Click one and look at the preview at the bottom of the Inspector.

> **Pink models?** That means the materials were made for a different render pipeline. Go to
> **Window → Rendering → Render Pipeline Converter**, tick **Material Upgrade**, click
> **Initialize Converters**, then **Convert Assets**.

## Step 4: Add my scripts

1. Unzip the zip file I gave you.
2. Copy the folder `Assets/ConveyorSystem` (the whole folder) into your project's `Assets` folder.
   You can drag it from Windows Explorer straight into Unity's Project window.
3. Copy `README.md` and `.gitignore` into the **project root folder** (the folder that holds `Assets`,
   `Packages` and `ProjectSettings`, not inside `Assets`).
4. Wait for Unity to finish compiling (spinner in the bottom-right corner).
   The **Console** (Window → General → Console) should show no red errors.

> **Optional but recommended:** go to **Edit → Project Settings → Player → Other Settings →
> Active Input Handling** and set it to **Both**. The scripts work either way, but this avoids
> surprises. Unity will restart.

## Step 5: Run the Setup Wizard (this builds everything for you)

1. In the top menu go to **Tools → Conveyor System → Setup Wizard**.
2. In the Project window, click the **straight conveyor model(s)** (Ctrl-click to select several),
   then click **Add selected as Straight** in the wizard.
3. Select the **curved conveyor model(s)** and click **Add selected as 90° Curve**.
   (Each curve automatically becomes a *Left* and a *Right* version.)
4. Anything else (merges, spirals, ramps and so on): **Add selected as Custom**.
5. Select the **product models** and click **Add selected products**.
6. Check the **Size** shown for each conveyor. It should be a sensible size in metres (for example 2 x 1 x 1).
   If the wizard warns that the model is too big, fix the model's **Scale Factor** as the warning says.
7. Click **Generate Prefabs + Build Scene**.

The wizard creates prefabs in `Assets/ConveyorSystem/Generated`, builds and saves the scene
`ConveyorScene`, and adds it to the build.

## Step 6: Press Play ▶

You should see a demo conveyor line with products moving on it. Try:
- Click a conveyor button on the left, move the mouse near the end of the line (it snaps), and click to place it.
- Press **R** to rotate, **right-click** to stop placing, and **right-click on a conveyor** to delete it.
- Click a placed conveyor to pick it up and move it.

### If a conveyor looks wrong (products float, go sideways, or go the wrong way)

The wizard *guesses* where each belt starts and ends. To fix a bad guess:
1. In `Assets/ConveyorSystem/Generated/Conveyors`, double-click the prefab to open **Prefab Mode**.
2. You'll see a green **ENTRY** sphere, a red **EXIT** sphere and a yellow path line.
3. Select the `Entry` child object and move it (W key = move tool) to the **top surface of the belt at its start**.
   Rotate it (E key) so its **blue arrow points the way products should travel**. Do the same for `Exit` at the end.
4. Quick fixes: right-click the **Conveyor Piece** component header and use
   - **Reverse Direction**: if products go backwards.
   - **Auto-fit Snap Points → Straight / Curve Left / Curve Right**: to re-run the guess.
5. Leave Prefab Mode (it auto-saves). Then run the wizard again with
   *"Overwrite prefabs that already exist"* **OFF** to rebuild the scene with your fixed prefabs.

## Step 7: Make the Windows build

1. **File → Build Profiles** (on Unity 2022 it's called **Build Settings**).
2. Choose **Windows** and check that `ConveyorScene` is ticked at the top of the scene list.
3. Click **Build** and create a new folder called `Build` inside your project folder.
4. When it finishes, open `Build`, double-click the `.exe` and check that it works.
5. Right-click the `Build` folder → **Send to → Compressed (zipped) folder**. Rename it to
   `VACAC_ConveyorSystem_Build.zip`.

## Step 8: Record the video demo (2–3 minutes)

* The easiest option is the **Xbox Game Bar**: run the build, then press **Win + Alt + R** to start and stop recording.
  The video is saved in `Videos\Captures`. OBS Studio is a good free alternative.
* Suggested script:
  1. The demo line with products running (5 seconds).
  2. Placing straights and curves, showing the snapping and the green/red preview.
  3. Building a second line. Products appear at every line start automatically.
  4. Products queuing when they're blocked, and products dropping off the end.
  5. Speed and spawn-rate sliders, choosing a product type, flow-path toggle.
  6. Moving a conveyor, deleting one (products fall off), Save → Clear all → Load.

## Step 9: Put it on GitHub (must be **public**)

1. Install **GitHub Desktop** (https://desktop.github.com) and sign in.
2. **File → Add local repository →** choose your Unity project folder. When it asks, click **create a repository**.
   (My `.gitignore` is already there, so the huge `Library` folder isn't uploaded.)
3. Write a commit message such as "Conveyor system" and click **Commit to main**.
4. Click **Publish repository**, **untick "Keep this code private"**, and publish.
5. On github.com, open your repository → **Releases → Draft a new release**. Set the tag to `v1.0`
   and attach `VACAC_ConveyorSystem_Build.zip` (and the video if you like). Publish it.
6. Put your GitHub link and video link into `README.md`, commit and push again.

> If GitHub refuses a file larger than 100 MB, it's usually a model or texture from the package.
> Install Git LFS (`git lfs install`) and track that file type, or ask me for help.

## Step 10: Submit

Upload these to the SharePoint **deliverables** folder:
1. `README.md` (or a PDF of it) with the GitHub link.
2. `VACAC_ConveyorSystem_Build.zip`.
3. The video file.
