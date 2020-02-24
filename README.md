# PiRhoDocGen
A Unity tool used to generate type information that can be compiled into documentation websites

## Installation

- In your Unity project open the Package Manager ('Window -> Package Manager')
- Click on the 'Add' (+) button in the top left and choose "Add package from git URL..."
- Enter the URL, https://github.com/pirhosoft/PiRhoDocGen.git#upm in the popup box and click 'Add'

## Updating

- Once installed, open the Packages/manifest.json file in a text editor
- At the bottom in a property named "lock", remove the object entry titled "com.pirho.utilties"
- Save and return to Unity and it will automatically reimport the updated version from the repository

## Usage

- Open up the Documentation Generator window in 'Window -> PiRho Soft -> Documentation Generator'