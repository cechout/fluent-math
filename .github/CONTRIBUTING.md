# Contributing to Simple Calculator

Thanks for wanting to contribute. Simple Calculator is a solo hobby project, so response times may vary. For anything beyond a small fix, open an issue first to talk through the approach before writing code, saves everyone rework.

## Getting set up

1. [Fork](https://github.com/cechout/simple-calculator/fork) the repository
2. Clone your fork

```
git clone https://github.com/<your-username>/simple-calculator.git
```

3. Create a branch off `main`, named `feature/xxx`, `fix/xxx`, or `chore/xxx`

```
git checkout -b feature/your-feature-name
```

4. Make your changes, then push and [open a pull request](https://github.com/cechout/simple-calculator/compare) against `main`

See the "How to Run" section in the [README](https://github.com/cechout/simple-calculator/blob/main/.github/README.md) for build prerequisites.

`Calculator_WinUI` is the project that is being developed. `Calculator` is the original WPF version 1.0.0 and is kept for history only, changes to it are not accepted.

## What we accept

* keep pull requests focused on one thing, and link the issue it addresses if there is one
* if it is a bug fix, check whether the same problem shows up elsewhere in the codebase before submitting
* avoid reformatting or restructuring code you are not otherwise touching, keep the diff to what you actually changed

## A few more things

AI tools are completely fine to use for writing code. Just review what you submit and be able to explain why it is written the way it is, PRs that are clearly unreviewed AI output will not get merged.

Commit messages should be short, imperative, single sentence, English, no body. Branch history does not need to be pristine either, commits get squashed into one on merge anyway.
