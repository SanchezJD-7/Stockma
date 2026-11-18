import styled from '@emotion/styled'

export const Viewport = styled.div`
  position: fixed;
  right: 16px;
  bottom: 16px;
  z-index: 1400;
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: min(380px, calc(100vw - 32px));
`

export const Toast = styled.div`
  border-radius: 8px;
  box-shadow: var(--color-shadow-lg);
`
