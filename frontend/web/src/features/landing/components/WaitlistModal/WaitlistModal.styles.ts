import styled from '@emotion/styled'
import { PrimaryButton } from '../../../../styles/components/buttons.styles'

export const SubmitButton = styled(PrimaryButton)`
  width: 100%;
  height: 44px;
  font-size: var(--fs-16);
  gap: 10px;
  background: var(--brand-teal);
  color: var(--color-white);

  &:hover:not(:disabled) {
    background: var(--brand-teal-active);
  }
`
