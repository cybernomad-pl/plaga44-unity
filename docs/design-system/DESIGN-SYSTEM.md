# CYBERNOMAD Design System
**Extracted from cbrnmd-wiki Vaadin prototype**
**Date:** 2025-10-29

## Brand Colors

From `cybernomad.pl` - Dark cyber aesthetic:

```css
--cbrnmd-background: #111          /* Near-black background */
--cbrnmd-background-dark: #000     /* Pure black */
--cbrnmd-primary: #3fc99a          /* Teal green (primary brand) */
--cbrnmd-primary-light: #43D079    /* Lighter teal */
--cbrnmd-cursor: #1fd1c1           /* Cyan accent */
--cbrnmd-secondary: #2A2A2A        /* Dark gray */
--cbrnmd-text: #3fc99a             /* Primary text (teal) */
--cbrnmd-text-secondary: #AAAAAA   /* Secondary text (gray) */
--cbrnmd-text-dim: #666            /* Dimmed text */
--cbrnmd-text-very-dim: #444       /* Very dim borders/dividers */
```

## Typography

**Primary Font:** `'Share Tech Mono', monospace`
- Google Fonts: https://fonts.googleapis.com/css?family=Share+Tech+Mono

**Font Sizes:**
- Article title: 1.6rem
- Section heading (h2): 1.2rem
- Subsection (h3): 1rem
- Body text: 0.9rem
- Metadata/labels: 0.75rem

## Title Format

**Game Title Style:**
```html
<span style='color: #fff;'>PLAGA</span><span style='color: #d4713d;'>'44</span>
```

- "PLAGA" = white (#fff)
- "'44" = orange accent (#d4713d)

## UI Components

### Hamburger Menu
- Position: top-left (20px, 20px)
- 3 lines, white (#fff)
- Hover effect: shows title overlay

### Wiki Layout (Fandom-style)
- **Left sidebar:** Navigation, categories
- **Center:** Article content
- **Right sidebar:** Table of contents, info boxes

### Scrollbar Styling
- Width: 8px
- Track: dark background
- Thumb: primary teal (#3fc99a)
- Hover: lighter teal (#43D079)

### Hover Effects
```css
background: rgba(63, 201, 154, 0.1);  /* 10% teal overlay */
border-left-color: var(--cbrnmd-primary);
```

## Notes

- **Full bleed design:** All backgrounds extend to viewport edges
- **Monospace aesthetic:** Consistent with cyberpunk/hacker theme
- **High contrast:** Dark backgrounds with bright teal accents
- **Minimal borders:** Very dim (#444) for subtle separation

## Usage

Apply to:
- Strapi admin panel customization
- Future cbrnmd.content frontend
- Unity game UI elements
- Android app theming

---

**Source:** `/home/bv/cbrnmd.content/design-archive/`
**Related:** WikiView.java line 72 (title format)
