import styled from '@emotion/styled'

const base = `
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  height: 36px;
  padding: 0 16px;
  border-radius: 24px;
  font-family: var(--font-family);
  font-size: var(--fs-14);
  font-weight: 500;
  white-space: nowrap;
  cursor: pointer;
  transition:
    background-color 120ms ease,
    color 120ms ease,
    border-color 120ms ease;

  &:disabled {
    opacity: 0.45;
    cursor: not-allowed;
  }
`

export const PrimaryButton = styled.button`
  ${base}
  border: none;
  background-color: var(--color-primary);
  color: var(--color-primary-contrast);

  &:hover:not(:disabled) {
    background-color: var(--color-primary-active);
  }
`

export const SecondaryButton = styled.button`
  ${base}
  border: 1.5px solid var(--color-primary);
  background-color: var(--color-bg-surface);
  color: var(--color-primary);

  &:hover:not(:disabled) {
    background-color: var(--color-primary-bg);
  }
`

export const GhostButton = styled.button`
  ${base}
  border: none;
  background-color: transparent;
  color: var(--color-primary);

  &:hover:not(:disabled) {
    background-color: var(--color-primary-bg);
  }
`
