// Expo replaces this public setting at build time; this is its local TypeScript shape.
declare const process: {
  readonly env: {
    readonly EXPO_PUBLIC_API_URL?: string
  }
}
