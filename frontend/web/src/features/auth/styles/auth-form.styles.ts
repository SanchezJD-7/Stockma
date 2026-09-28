import styled from '@emotion/styled'
import { GhostButton, PrimaryButton } from '../../../styles/components/buttons'
import { MutedText, SectionTitle } from '../../../styles/components/texts'

export const FormTitle = styled(SectionTitle)`
  margin: 0 calc(-1 * var(--card-padding-x));
  padding: 8px var(--card-padding-x);
  background: var(--brand-navy);
  color: var(--color-white);
  text-align: center;
`

export const FormBody = styled.div`
  display: flex;
  flex-direction: column;
  gap: 20px;
  margin-top: 20px;
`

export const Hint = styled(MutedText)`
  text-align: center;
`

export const SubmitButton = styled(PrimaryButton)`
  width: 100%;
  height: 44px;
  font-size: var(--fs-16);
`

export const BackButton = styled(GhostButton)`
  width: 100%;
  height: 44px;
  font-size: var(--fs-16);
`

export const ActionRow = styled.div`
  display: flex;
  gap: 12px;

  & > * {
    flex: 1;
  }
`
